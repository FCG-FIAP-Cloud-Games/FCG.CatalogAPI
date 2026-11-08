using FCG.Catalog.Application.Orders;
using FCG.Catalog.Domain.Orders;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace FCG.Catalog.Infrastructure.Repositories;

public sealed class RepositorioPedidos(CatalogDbContext db) : IRepositorioPedidos
{
    public Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Pedidos.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    public Task<Pedido?> ObterPorChaveAsync(Guid userId, Guid key, CancellationToken cancellationToken = default) =>
        db.Pedidos.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId && p.IdempotencyKey == key, cancellationToken);
    public Task<Pedido?> ObterPendenteAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default) =>
        db.Pedidos.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId && p.GameId == gameId && p.Status == StatusPedido.PendingPayment, cancellationToken);
    public async Task<ITransacaoPedido> IniciarTransacaoAsync(CancellationToken cancellationToken = default) =>
        new Transacao(await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken));
    public async Task AdicionarAsync(Pedido pedido, CancellationToken cancellationToken = default)
    {
        db.Pedidos.Add(pedido);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_pedidos_usuario_chave" or "ux_pedidos_usuario_jogo_pendente" })
        {
            db.Entry(pedido).State = EntityState.Detached;
            throw new ConflitoUnicoPedidoException(ex);
        }
    }
    private sealed class Transacao(IDbContextTransaction transaction) : ITransacaoPedido
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
