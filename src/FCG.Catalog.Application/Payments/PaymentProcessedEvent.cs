namespace FCG.Catalog.Application.Payments;

public sealed record PaymentProcessedEvent
{
    public Guid EventId { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public int Version { get; init; }
    public Guid PaymentId { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public Guid GameId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

// Códigos estáveis, sem payload nem mensagens de infraestrutura.
public sealed class PagamentoInvalidoException(string motivo) : Exception(motivo)
{
    public string Motivo { get; } = motivo;
}

public static class ValidadorPaymentProcessedEvent
{
    public static void Validar(PaymentProcessedEvent e)
    {
        if (e.EventId == Guid.Empty || e.CorrelationId == Guid.Empty || e.PaymentId == Guid.Empty
            || e.OrderId == Guid.Empty || e.UserId == Guid.Empty || e.GameId == Guid.Empty)
            throw new PagamentoInvalidoException("IdentificadorInvalido");
        if (e.Version != 1) throw new PagamentoInvalidoException("VersaoIncompativel");
        if (e.OccurredAt == default) throw new PagamentoInvalidoException("OccurredAtInvalido");
        if (e.Amount <= 0) throw new PagamentoInvalidoException("AmountInvalido");
        // Pedido persiste moeda em três letras ASCII maiúsculas (BRL); sem conversão cambial.
        if (e.Currency is null || e.Currency.Length != 3 || e.Currency.Any(c => c is < 'A' or > 'Z'))
            throw new PagamentoInvalidoException("CurrencyInvalida");
        if (e.Status is not ("Approved" or "Rejected"))
            throw new PagamentoInvalidoException("StatusInvalido");
    }
}
