namespace FCG.Catalog.Domain.Orders;

public enum StatusPedido { PendingPayment, Paid, Rejected }

public sealed class Pedido
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public StatusPedido Status { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Pedido() { }

    public Pedido(Guid userId, Guid gameId, decimal price, Guid idempotencyKey)
    {
        if (userId == Guid.Empty || gameId == Guid.Empty || idempotencyKey == Guid.Empty)
            throw new ArgumentException("Usuário, jogo e chave de idempotência são obrigatórios.");
        if (price < 0 || decimal.Round(price, 2) != price)
            throw new ArgumentException("Preço deve ser não negativo e ter no máximo duas casas decimais.");
        Id = Guid.NewGuid();
        UserId = userId;
        GameId = gameId;
        Price = price;
        IdempotencyKey = idempotencyKey;
        Status = StatusPedido.PendingPayment;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Finalizar(StatusPedido status)
    {
        if (Status != StatusPedido.PendingPayment || status is not (StatusPedido.Paid or StatusPedido.Rejected))
            throw new InvalidOperationException("Somente pedidos pendentes podem ser finalizados como Paid ou Rejected.");
        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
