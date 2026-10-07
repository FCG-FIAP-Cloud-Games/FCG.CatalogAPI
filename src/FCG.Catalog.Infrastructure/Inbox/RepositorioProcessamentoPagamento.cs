using FCG.Catalog.Application.Orders;
using FCG.Catalog.Application.Payments;
using FCG.Catalog.Domain.Library;
using FCG.Catalog.Domain.Orders;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FCG.Catalog.Infrastructure.Inbox;

public sealed class RepositorioProcessamentoPagamento(CatalogDbContext db) : IRepositorioProcessamentoPagamento
{
    public async Task<ITransacaoPedido> IniciarTransacaoAsync(CancellationToken ct) =>
        new Transacao(db, await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct));

    public Task<bool> ProcessadoAsync(string consumerName, Guid eventId, CancellationToken ct) =>
        db.Inbox.AsNoTracking().AnyAsync(m => m.ConsumerName == consumerName && m.EventId == eventId, ct);

    public async Task<Pedido?> ObterPedidoAsync(Guid id, CancellationToken ct)
    {
        // Recarrega inclusive se o contexto já tinha a entidade antes de adquirir o lock.
        var pedido = await db.Pedidos.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (pedido is not null) await db.Entry(pedido).ReloadAsync(ct);
        return pedido;
    }

    public Task<Aquisicao?> ObterPosseAsync(Guid userId, Guid gameId, CancellationToken ct) =>
        db.Aquisicoes.AsNoTracking().SingleOrDefaultAsync(a => a.UsuarioId == userId && a.JogoId == gameId, ct);
    public Task<Aquisicao?> ObterPorPedidoAsync(Guid pedidoId, CancellationToken ct) =>
        db.Aquisicoes.AsNoTracking().SingleOrDefaultAsync(a => a.PedidoId == pedidoId, ct);

    public void Registrar(string consumerName, Guid eventId) => db.Inbox.Add(new MensagemInbox(consumerName, eventId));

    private sealed class Transacao(CatalogDbContext db, IDbContextTransaction tx) : ITransacaoPedido
    {
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        public async ValueTask DisposeAsync()
        {
            await tx.DisposeAsync();
            db.ChangeTracker.Clear();
        }
    }
}
