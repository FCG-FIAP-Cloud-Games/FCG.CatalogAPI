using FCG.Catalog.Application.Library;
using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Domain.Library;
using FCG.Catalog.Domain.Orders;
using FCG.Catalog.Infrastructure.Data.EF;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using FCG.Catalog.Infrastructure.Repositories;
using FCG.Catalog.Tests;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace FCG.Catalog.IntegrationTests;

public sealed class PostgreSqlBibliotecaTests
{
    internal static async Task<Pedido> Seed(CatalogDbContext db)
    {
        var game = new Jogo(Guid.NewGuid(), "Biblioteca", null, null, 20);
        var order = new Pedido(Guid.NewGuid(), game.Id, 20, Guid.NewGuid());
        db.Jogos.Add(game); db.Pedidos.Add(order); await db.SaveChangesAsync();
        return order;
    }

    [PostgreSqlFact]
    public async Task Constraints_reais_historicos_fks_e_restrict()
    {
        await using var env = new BibliotecaDatabase(); await env.InitializeAsync();
        await using var db = env.Open(); var order = await Seed(db); var other = await Seed(db);
        var a = Aquisicao.Conceder(order.UserId, order.GameId, order.Id);
        db.Aquisicoes.Add(a); await db.SaveChangesAsync();
        async Task Constraint(Aquisicao row, string name)
        {
            db.Aquisicoes.Add(row);
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(name, Assert.IsType<PostgresException>(ex.InnerException).ConstraintName);
            db.ChangeTracker.Clear();
        }
        await Constraint(Aquisicao.Historica(Guid.NewGuid(), order.UserId, order.GameId, DateTimeOffset.UtcNow), "ux_aquisicoes_usuario_jogo");
        await Constraint(Aquisicao.Conceder(other.UserId, other.GameId, order.Id), "ux_aquisicoes_pedido");
        await Constraint(Aquisicao.Conceder(Guid.NewGuid(), Guid.NewGuid(), other.Id), "fk_aquisicoes_jogos");
        await Constraint(Aquisicao.Conceder(Guid.NewGuid(), other.GameId, Guid.NewGuid()), "fk_aquisicoes_pedidos");
        db.Aquisicoes.AddRange(
            Aquisicao.Historica(Guid.NewGuid(), Guid.NewGuid(), order.GameId, DateTimeOffset.UtcNow),
            Aquisicao.Historica(Guid.NewGuid(), Guid.NewGuid(), order.GameId, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.Aquisicoes.CountAsync(x => x.PedidoId == null));
        await using var connection = new NpgsqlConnection(env.ConnectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid = 'aquisicoes'::regclass AND contype = 'f'", connection);
        var fks = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) fks.Add(reader.GetString(0));
        Assert.Equal(2, fks.Count);
        Assert.Contains("FOREIGN KEY (jogo_id) REFERENCES jogos(id) ON DELETE RESTRICT", fks);
        Assert.Contains("FOREIGN KEY (pedido_id) REFERENCES pedidos(\"Id\") ON DELETE RESTRICT", fks);
        await using var deleteOrder = new NpgsqlCommand("DELETE FROM pedidos WHERE \"Id\" = @id", connection);
        deleteOrder.Parameters.AddWithValue("id", order.Id);
        Assert.Equal("fk_aquisicoes_pedidos", (await Assert.ThrowsAsync<PostgresException>(() => deleteOrder.ExecuteNonQueryAsync())).ConstraintName);
        // Jogo sem pedido: a restrição só pode vir da aquisição histórica.
        var onlyGame = new Jogo(Guid.NewGuid(), "Histórico", null, null, 1);
        db.Jogos.Add(onlyGame); db.Aquisicoes.Add(Aquisicao.Historica(Guid.NewGuid(), Guid.NewGuid(), onlyGame.Id, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        await using var deleteGame = new NpgsqlCommand("DELETE FROM jogos WHERE id = @id", connection);
        deleteGame.Parameters.AddWithValue("id", onlyGame.Id);
        Assert.Equal("fk_aquisicoes_jogos", (await Assert.ThrowsAsync<PostgresException>(() => deleteGame.ExecuteNonQueryAsync())).ConstraintName);
    }

    [PostgreSqlFact]
    public async Task Transacao_externa_visibilidade_rollback_lock_e_repeticao()
    {
        await using var env = new BibliotecaDatabase(); await env.InitializeAsync();
        await using var db = env.Open(); var order = await Seed(db);
        var repo = new RepositorioAquisicoes(db); var query = new ConsultaBiblioteca(db);
        var row = Aquisicao.Conceder(order.UserId, order.GameId, order.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.InserirAsync(row));
        Assert.False(await query.PossuiJogoAsync(order.UserId, order.GameId));
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await new LockUsuarioJogo(db).AcquireAsync(order.UserId, order.GameId);
            var games = new PedidoFakes(); games.Jogos.Add(await db.Jogos.SingleAsync());
            var handler = new ManipuladorConcederJogoAoUsuario(games, new RepositorioPedidos(db), repo);
            Assert.Equal(ResultadoConcessao.Concedido, await handler.ExecutarAsync(order.UserId, order.GameId, order.Id));
            Assert.Equal(ResultadoConcessao.JaConcedido, await handler.ExecutarAsync(order.UserId, order.GameId, order.Id));
            Assert.True(await query.PossuiJogoAsync(order.UserId, order.GameId));
            Assert.False(await query.PossuiJogoAsync(Guid.NewGuid(), order.GameId));
            Assert.Same(tx, db.Database.CurrentTransaction);
            await using var observer = env.Open();
            Assert.Empty(await observer.Aquisicoes.ToListAsync());
            await tx.RollbackAsync();
        }
        Assert.False(await query.PossuiJogoAsync(order.UserId, order.GameId));
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            Assert.True(await repo.InserirAsync(row));
            Assert.False(await repo.InserirAsync(Aquisicao.Conceder(order.UserId, order.GameId, order.Id)));
            Assert.Equal(1, await db.Aquisicoes.CountAsync()); // Conflito não abortou a transação.
            await tx.CommitAsync();
        }
        await using var final = env.Open(); Assert.Single(await final.Aquisicoes.ToListAsync());
        Assert.Equal(StatusPedido.PendingPayment, (await final.Pedidos.SingleAsync()).Status);
        Assert.Empty(await final.Outbox.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task Corrida_de_concessao_sem_lock_nao_aborta_transacao()
    {
        await using var env = new BibliotecaDatabase(); await env.InitializeAsync();
        await using var first = env.Open(); var order = await Seed(first);
        await using var second = env.Open();
        await using var tx1 = await first.Database.BeginTransactionAsync();
        await using var tx2 = await second.Database.BeginTransactionAsync();
        var game = await first.Jogos.SingleAsync();
        var checkedBoth = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        async Task WaitForBoth()
        {
            if (Interlocked.Increment(ref count) == 2) checkedBoth.TrySetResult();
            await checkedBoth.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        async Task<ResultadoConcessao> Grant(CatalogDbContext db)
        {
            var fake = new PedidoFakes(); fake.Jogos.Add(game);
            var repository = new BarrierRepository(new RepositorioAquisicoes(db), WaitForBoth);
            var result = await new ManipuladorConcederJogoAoUsuario(fake, new RepositorioPedidos(db), repository)
                .ExecutarAsync(order.UserId, order.GameId, order.Id);
            Assert.Equal(1, await db.Aquisicoes.CountAsync());
            await db.Database.CurrentTransaction!.CommitAsync();
            return result;
        }
        var results = await Task.WhenAll(Grant(first), Grant(second)).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(results, x => x == ResultadoConcessao.Concedido);
        Assert.Single(results, x => x == ResultadoConcessao.JaConcedido);
        await using var observer = env.Open(); Assert.Single(await observer.Aquisicoes.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task Historico_preservado_e_pedido_reutilizado_nao_vira_sucesso()
    {
        await using var env = new BibliotecaDatabase(); await env.InitializeAsync();
        await using var db = env.Open(); var order = await Seed(db); var other = await Seed(db);
        var historical = Aquisicao.Historica(Guid.NewGuid(), order.UserId, order.GameId,
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        db.Aquisicoes.Add(historical);
        db.Aquisicoes.Add(Aquisicao.Conceder(other.UserId, other.GameId, other.Id));
        await db.SaveChangesAsync();
        var fake = new PedidoFakes(); fake.Jogos.Add(await db.Jogos.SingleAsync(g => g.Id == order.GameId));
        var repo = new RepositorioAquisicoes(db);
        await using var tx = await db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => repo.InserirAsync(historical));
        var handler = new ManipuladorConcederJogoAoUsuario(fake, new RepositorioPedidos(db), repo);
        Assert.Equal(ResultadoConcessao.JaConcedido, await handler.ExecutarAsync(order.UserId, order.GameId, order.Id));
        var actual = await db.Aquisicoes.AsNoTracking().SingleAsync(a => a.UsuarioId == order.UserId);
        Assert.Equal(historical.Id, actual.Id); Assert.Equal(historical.DataAquisicao, actual.DataAquisicao);
        Assert.Null(actual.PedidoId);
        var error = await Assert.ThrowsAsync<PostgresException>(() => repo.InserirAsync(
            Aquisicao.Conceder(Guid.NewGuid(), order.GameId, other.Id)));
        Assert.Equal("ux_aquisicoes_pedido", error.ConstraintName);
        Assert.Equal(2, await db.Aquisicoes.CountAsync());
        await tx.RollbackAsync();
    }
    private sealed class BarrierRepository(IRepositorioAquisicoes inner, Func<Task> wait) : IRepositorioAquisicoes
    {
        public async Task<bool> ExisteAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
        { var exists = await inner.ExisteAsync(userId, gameId, cancellationToken); await wait(); return exists; }
        public Task<bool> InserirAsync(Aquisicao aquisicao, CancellationToken cancellationToken = default) => inner.InserirAsync(aquisicao, cancellationToken);
    }
}
