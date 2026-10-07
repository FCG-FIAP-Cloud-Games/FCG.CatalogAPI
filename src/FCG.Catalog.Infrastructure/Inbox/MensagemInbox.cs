namespace FCG.Catalog.Infrastructure.Inbox;

public sealed class MensagemInbox
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid EventId { get; private set; }
    public string ConsumerName { get; private set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; private set; }

    private MensagemInbox() { }
    public MensagemInbox(string consumerName, Guid eventId)
    {
        ConsumerName = consumerName;
        EventId = eventId;
        ProcessedAt = DateTimeOffset.UtcNow;
    }
}
