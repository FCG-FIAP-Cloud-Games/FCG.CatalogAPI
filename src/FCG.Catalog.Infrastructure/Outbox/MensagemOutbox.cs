using System.Text.Json;
using FCG.Catalog.Application.Orders;
namespace FCG.Catalog.Infrastructure.Outbox;

public sealed class MensagemOutbox
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public int Attempts { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid CorrelationId { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public Guid? LockToken { get; private set; }
    private MensagemOutbox() { }
    public static MensagemOutbox De(OrderPlacedEvent e) => new()
    {
        Id = Guid.NewGuid(), EventId = e.EventId, EventType = nameof(OrderPlacedEvent),
        Payload = JsonSerializer.Serialize(e), OccurredAt = e.OccurredAt,
        OrderId = e.OrderId, CorrelationId = e.CorrelationId
    };
}

public interface IRepositorioOutbox : IRegistroOutbox
{
    Task<IReadOnlyList<MensagemOutbox>> ReservarAsync(int tamanho, TimeSpan duracao, CancellationToken ct);
    Task<bool> PublicadaAsync(Guid id, Guid token, CancellationToken ct);
    Task<bool> FalhouAsync(Guid id, Guid token, TimeSpan atraso, string erro, CancellationToken ct);
}

public interface IPublicadorOutbox
{
    Task PublicarAsync(MensagemOutbox mensagem, CancellationToken ct);
}
