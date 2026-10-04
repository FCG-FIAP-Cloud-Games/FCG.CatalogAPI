using FCG.Catalog.Domain.Orders;

namespace FCG.Catalog.Application.Orders;

// C18 fornecerá a implementação real; nenhum fallback de posse em produção.
public interface IConsultaBiblioteca
{
    Task<bool> PossuiJogoAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);
}

public interface ILockUsuarioJogo
{
    Task AcquireAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);
}

public interface ITransacaoPedido : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

public interface IRepositorioPedidos
{
    Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Pedido?> ObterPorChaveAsync(Guid userId, Guid key, CancellationToken cancellationToken = default);
    Task<Pedido?> ObterPendenteAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);
    Task<ITransacaoPedido> IniciarTransacaoAsync(CancellationToken cancellationToken = default);
    Task AdicionarAsync(Pedido pedido, CancellationToken cancellationToken = default);
}

public sealed class ConflitoUnicoPedidoException(Exception inner) : Exception("Conflito de unicidade do pedido.", inner);
public sealed record ResultadoPedido(Pedido? Pedido, int Codigo, string? Erro = null);
