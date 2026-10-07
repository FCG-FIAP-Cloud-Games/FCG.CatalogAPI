using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
namespace FCG.Catalog.Infrastructure.Outbox;

public sealed class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Exchange { get; set; } = "OrderPlacedEvent";
    public bool ConsumerEnabled { get; set; } = true;
    public string PaymentProcessedExchange { get; set; } = "PaymentProcessedEvent";
    public string PaymentProcessedQueue { get; set; } = "catalog-payment-processed";
    public string PaymentProcessedErrorQueue { get; set; } = "catalog-payment-processed-error";
}

public sealed class RabbitMqPublisher(IOptions<RabbitMqOptions> options) : IPublicadorOutbox, IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IConnection? connection;
    private IChannel? channel;

    public async Task PublicarAsync(MensagemOutbox mensagem, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            var o = options.Value;
            if (string.IsNullOrWhiteSpace(o.Username) || string.IsNullOrWhiteSpace(o.Password))
                throw new InvalidOperationException("Credenciais RabbitMq não configuradas.");
            if (channel?.IsOpen != true)
            {
                await ResetAsync();
                var factory = new ConnectionFactory
                {
                    HostName = o.Host, Port = o.Port, VirtualHost = o.VirtualHost,
                    UserName = o.Username, Password = o.Password,
                    // O próximo retry recria conexão/canal; não há recuperação de bindings do consumer.
                    AutomaticRecoveryEnabled = false,
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
                    ContinuationTimeout = TimeSpan.FromSeconds(5)
                };
                connection = await factory.CreateConnectionAsync(ct);
                channel = await connection.CreateChannelAsync(new CreateChannelOptions(
                    publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
                await channel.ExchangeDeclareAsync(o.Exchange, ExchangeType.Fanout,
                    durable: true, autoDelete: false, cancellationToken: ct);
            }
            var properties = new BasicProperties
            {
                Persistent = true, ContentType = "application/json", Type = mensagem.EventType,
                MessageId = mensagem.EventId.ToString(), CorrelationId = mensagem.CorrelationId.ToString()
            };
            // O tracking do cliente 7 correlaciona BasicReturn e Nack com a publicação e
            // faz este await falhar. Um ACK posterior ao retorno não transforma isso em sucesso.
            await channel.BasicPublishAsync(o.Exchange, "", mandatory: true,
                basicProperties: properties, body: Encoding.UTF8.GetBytes(mensagem.Payload), cancellationToken: ct);
        }
        catch
        {
            await ResetAsync(); // Timeout/erro invalida o canal; próximo envio parte de estado conhecido.
            throw;
        }
        finally { gate.Release(); }
    }

    private async Task ResetAsync()
    {
        var oldChannel = channel; var oldConnection = connection;
        channel = null; connection = null;
        if (oldChannel is not null) { try { await oldChannel.DisposeAsync(); } catch { /* conexão já encerrada */ } }
        if (oldConnection is not null) { try { await oldConnection.DisposeAsync(); } catch { /* conexão já encerrada */ } }
    }
    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try { await ResetAsync(); }
        finally { gate.Release(); }
        gate.Dispose();
    }
}
