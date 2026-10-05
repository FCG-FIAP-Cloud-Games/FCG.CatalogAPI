using FCG.Catalog.Application.Library;
using FCG.Catalog.Domain.Library;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FCG.Catalog.Infrastructure.Repositories;

public sealed class RepositorioAquisicoes(CatalogDbContext db) : IRepositorioAquisicoes
{
    public Task<bool> ExisteAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default) =>
        db.Aquisicoes.AsNoTracking().AnyAsync(a => a.UsuarioId == userId && a.JogoId == gameId, cancellationToken);

    public async Task<bool> InserirAsync(Aquisicao aquisicao, CancellationToken cancellationToken = default)
    {
        var transaction = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Concessão exige transação ativa no mesmo CatalogDbContext.");
        if (aquisicao.PedidoId is null)
            throw new ArgumentException("Nova concessão exige PedidoId.", nameof(aquisicao));
        // O índice de pedido também pode detectar uma corrida do MESMO pedido antes
        // do arbiter de posse. O savepoint preserva a transação externa nesse caso.
        var savepoint = "c18_aquisicao_" + Guid.NewGuid().ToString("N");
        await transaction.CreateSavepointAsync(savepoint, cancellationToken);
        try
        {
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO aquisicoes (id, usuario_id, jogo_id, data_aquisicao, pedido_id)
                VALUES ({aquisicao.Id}, {aquisicao.UsuarioId}, {aquisicao.JogoId}, {aquisicao.DataAquisicao}, {aquisicao.PedidoId})
                ON CONFLICT (usuario_id, jogo_id) DO NOTHING
                """, cancellationToken);
            await transaction.ReleaseSavepointAsync(savepoint, cancellationToken);
            return inserted == 1;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation
            && ex.ConstraintName == "ux_aquisicoes_pedido")
        {
            await transaction.RollbackToSavepointAsync(savepoint, cancellationToken);
            await transaction.ReleaseSavepointAsync(savepoint, cancellationToken);
            // Somente repetição do mesmo pedido/par é sucesso. Pedido ligado a
            // outro titular/jogo continua sendo violação de integridade.
            if (await db.Aquisicoes.AsNoTracking().AnyAsync(a => a.UsuarioId == aquisicao.UsuarioId
                && a.JogoId == aquisicao.JogoId && a.PedidoId == aquisicao.PedidoId, cancellationToken))
                return false;
            throw;
        }
    }
}
