using FCG.Catalog.Application.Orders;
using FCG.Catalog.Domain.Library;
using FCG.Catalog.Domain.Orders;

namespace FCG.Catalog.Application.Payments;

public interface IRepositorioProcessamentoPagamento
{
    Task<ITransacaoPedido> IniciarTransacaoAsync(CancellationToken ct);
    Task<bool> ProcessadoAsync(string consumerName, Guid eventId, CancellationToken ct);
    Task<Pedido?> ObterPedidoAsync(Guid id, CancellationToken ct);
    Task<Aquisicao?> ObterPosseAsync(Guid userId, Guid gameId, CancellationToken ct);
    Task<Aquisicao?> ObterPorPedidoAsync(Guid pedidoId, CancellationToken ct);
    void Registrar(string consumerName, Guid eventId);
}

public enum ResultadoPagamento { Processado, Duplicado, Compativel }

public interface IProcessadorPagamento
{
    Task<ResultadoPagamento> ExecutarAsync(PaymentProcessedEvent evento, CancellationToken ct);
}
