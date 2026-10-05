using FCG.Catalog.Domain.Orders;
namespace FCG.Catalog.Application.Orders;

public sealed record OrderPlacedEvent(Guid EventId, Guid CorrelationId, DateTimeOffset OccurredAt,
    int Version, Guid OrderId, Guid UserId, Guid GameId, decimal Price, string Currency)
{
    public static OrderPlacedEvent De(Pedido pedido, Guid correlationId) =>
        new(Guid.NewGuid(), correlationId, pedido.CreatedAt.ToUniversalTime(), 1,
            pedido.Id, pedido.UserId, pedido.GameId, pedido.Price, pedido.Currency);
}

public interface IRegistroOutbox
{
    Task AdicionarAsync(OrderPlacedEvent evento, CancellationToken ct = default);
}

public interface IContextoCorrelacao { Guid CorrelationId { get; } }
public sealed class ContextoCorrelacao : IContextoCorrelacao
{
    public Guid CorrelationId { get; private set; } = Guid.NewGuid();
    public void Definir(IReadOnlyList<string> valores)
    {
        CorrelationId = valores.Count == 1 && Guid.TryParse(valores[0], out var id) && id != Guid.Empty
            ? id : Guid.NewGuid();
    }
}

public static class BackoffOutbox
{
    public static TimeSpan Calcular(int attempts) => TimeSpan.FromSeconds(attempts switch
    { <= 1 => 2, 2 => 5, 3 => 15, 4 => 30, _ => 60 });
}
