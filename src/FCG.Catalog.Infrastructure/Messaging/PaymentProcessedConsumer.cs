using FCG.Catalog.Application.Orders;
using FCG.Catalog.Application.Payments;
using FCG.Catalog.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FCG.Catalog.Infrastructure.Messaging;

public sealed class PaymentProcessedConsumer(IServiceScopeFactory scopes, IOptions<RabbitMqOptions> options,
    ILogger<PaymentProcessedConsumer> logger) : BackgroundService
{
    public const string AttemptHeader = "x-catalog-payment-attempt";
    public const string ErrorHeader = "x-catalog-payment-error";
    public const int MaxAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        if (!options.Value.ConsumerEnabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ConsumirAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Consumer PaymentProcessed indisponível: {ErrorType}; reconexão em 2s.", ex.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task ConsumirAsync(CancellationToken ct)
    {
        var o = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = o.Host, Port = o.Port, VirtualHost = o.VirtualHost,
            UserName = o.Username, Password = o.Password,
            AutomaticRecoveryEnabled = false, ConsumerDispatchConcurrency = 1,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5), ContinuationTimeout = TimeSpan.FromSeconds(5)
        };
        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        await channel.ExchangeDeclareAsync(o.PaymentProcessedExchange, ExchangeType.Fanout, true, false, cancellationToken: ct);
        await channel.QueueDeclareAsync(o.PaymentProcessedQueue, true, false, false, cancellationToken: ct);
        await channel.QueueDeclareAsync(o.PaymentProcessedErrorQueue, true, false, false, cancellationToken: ct);
        await channel.QueueBindAsync(o.PaymentProcessedQueue, o.PaymentProcessedExchange, "", cancellationToken: ct);
        await channel.BasicQosAsync(0, 1, false, ct);

        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connection.ConnectionShutdownAsync += (_, _) => { ended.TrySetResult(); return Task.CompletedTask; };
        channel.ChannelShutdownAsync += (_, _) => { ended.TrySetResult(); return Task.CompletedTask; };
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.UnregisteredAsync += (_, _) => { ended.TrySetResult(); return Task.CompletedTask; };
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try { await ProcessarEntregaAsync(channel, delivery, session.Token); }
            catch (OperationCanceledException) when (session.IsCancellationRequested) { }
            catch (Exception ex)
            {
                // Sem confirmação do handoff/ACK: fecha sessão e deixa o broker reentregar.
                logger.LogWarning("Entrega interrompida sem ACK: {ErrorType}.", ex.GetType().Name);
                ended.TrySetResult();
            }
        };
        await channel.BasicConsumeAsync(o.PaymentProcessedQueue, autoAck: false, consumer, ct);
        try { await ended.Task.WaitAsync(ct); }
        finally { await session.CancelAsync(); }
    }

    private async Task ProcessarEntregaAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken ct)
    {
        // O buffer de RabbitMQ.Client só é válido durante este callback.
        var body = delivery.Body.ToArray();
        PaymentProcessedEvent? evento = null;
        string? failure = null;
        var attempt = 1;
        var permanent = false;
        try
        {
            attempt = LerTentativa(delivery.BasicProperties.Headers);
            evento = AdaptadorPaymentProcessedEvent.Ler(body, delivery.BasicProperties.ContentType);
            using var logScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = evento.CorrelationId, ["EventId"] = evento.EventId,
                ["PaymentId"] = evento.PaymentId, ["OrderId"] = evento.OrderId, ["Status"] = evento.Status
            });
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ContextoCorrelacao>().DefinirRecebido(evento.CorrelationId);
            var resultado = await scope.ServiceProvider.GetRequiredService<IProcessadorPagamento>().ExecutarAsync(evento, ct);
            // O processador somente retorna após commit; falha no ACK pode causar redelivery idempotente.
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
            Log(evento, resultado.ToString(), attempt);
            return;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (PagamentoInvalidoException ex) { failure = ex.Motivo; permanent = true; }
        catch (Exception ex) { failure = ex.GetType().Name; }

        var errorQueue = permanent || attempt >= MaxAttempts;
        var properties = new BasicProperties(delivery.BasicProperties)
        {
            Persistent = true,
            Headers = delivery.BasicProperties.Headers is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(delivery.BasicProperties.Headers)
        };
        properties.Headers[AttemptHeader] = errorQueue ? attempt : attempt + 1;
        properties.Headers[ErrorHeader] = failure;
        if (evento is not null)
        {
            properties.Headers["x-catalog-event-id"] = evento.EventId.ToString();
            properties.Headers["x-catalog-correlation-id"] = evento.CorrelationId.ToString();
        }
        if (!errorQueue) await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct);
        var destination = errorQueue ? options.Value.PaymentProcessedErrorQueue : options.Value.PaymentProcessedQueue;
        // Publicação persistente com mandatory + publisher confirms. Se falhar, NÃO remove a original.
        await channel.BasicPublishAsync("", destination, mandatory: true, basicProperties: properties, body: body, cancellationToken: ct);
        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
        Log(evento, (errorQueue ? "FilaErro:" : "Retry:") + failure, attempt);
    }

    private static int LerTentativa(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(AttemptHeader, out var value)) return 1;
        return value switch
        {
            int i when i is >= 1 and <= MaxAttempts => i,
            long l when l is >= 1 and <= MaxAttempts => (int)l,
            _ => throw new PagamentoInvalidoException("TentativaInvalida")
        };
    }

    private void Log(PaymentProcessedEvent? e, string resultado, int attempt) =>
        logger.LogInformation("PaymentProcessed EventId={EventId} CorrelationId={CorrelationId} PaymentId={PaymentId} OrderId={OrderId} Status={Status} Tentativa={Tentativa} Resultado={Resultado}",
            e?.EventId, e?.CorrelationId, e?.PaymentId, e?.OrderId, e?.Status, attempt, resultado);
}
