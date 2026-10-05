using FCG.Catalog.Application.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace FCG.Catalog.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public bool Enabled { get; set; } = true;
}

// Uma mensagem por reserva: nenhuma mensagem espera outra publicação com lease correndo.
public sealed class EntregadorOutbox(IRepositorioOutbox repository, IPublicadorOutbox publisher,
    ILogger<EntregadorOutbox> logger)
{
    public async Task<int> ExecutarAsync(CancellationToken ct)
    {
        var lote = await repository.ReservarAsync(1, TimeSpan.FromSeconds(60), ct);
        foreach (var m in lote)
        {
            bool gravado;
            string resultado;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                await publisher.PublicarAsync(m, timeout.Token);
                gravado = await repository.PublicadaAsync(m.Id, m.LockToken!.Value, ct);
                resultado = gravado ? "publicada" : "reserva perdida após confirmação";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // Somente tipo técnico: mensagens de exceção podem conter credenciais/URI.
                var erro = ex is RabbitMQ.Client.Exceptions.PublishException publication
                    ? (publication.IsReturn ? "Unroutable" : "PublisherNack") : ex.GetType().Name;
                gravado = await repository.FalhouAsync(m.Id, m.LockToken!.Value,
                    BackoffOutbox.Calcular(m.Attempts), erro, ct);
                resultado = gravado ? "pendente: " + erro : "reserva perdida após falha";
            }
            logger.LogInformation("Outbox EventId={EventId} CorrelationId={CorrelationId} OrderId={OrderId} EventType={EventType} Attempts={Attempts} Resultado={Resultado}",
                m.EventId, m.CorrelationId, m.OrderId, m.EventType, m.Attempts, resultado);
        }
        return lote.Count;
    }
}

public sealed class OutboxWorker(IServiceScopeFactory scopes, IOptions<OutboxOptions> options,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Nenhuma conexão é aberta no caminho síncrono de startup do host.
        await Task.Yield();
        if (!options.Value.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            var count = 0;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                count = await scope.ServiceProvider.GetRequiredService<EntregadorOutbox>().ExecutarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Outbox indisponível: {ErrorType}; nova consulta será feita.", ex.GetType().Name);
            }
            if (count == 0)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
