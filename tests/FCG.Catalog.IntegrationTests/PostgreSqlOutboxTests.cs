using System.Text.Json;
using FCG.Catalog.Application.Orders;
using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Infrastructure.Data.EF;
using FCG.Catalog.Infrastructure.Outbox;
using FCG.Catalog.Infrastructure.Repositories;
using FCG.Catalog.Tests;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
namespace FCG.Catalog.IntegrationTests;

public sealed class PostgreSqlOutboxTests
{
    [PostgreSqlFact]
    public async Task Workers_concorrentes_reservam_mensagens_distintas()
    {
        await using var env = new OutboxDatabase(); await env.InitializeAsync();
        await env.CriarAsync(); await env.CriarAsync();
        await using var a = env.Open(); await using var b = env.Open();
        var results = await Task.WhenAll(
            new RepositorioOutbox(a).ReservarAsync(1, TimeSpan.FromMinutes(1), default),
            new RepositorioOutbox(b).ReservarAsync(1, TimeSpan.FromMinutes(1), default));
        Assert.NotEqual(Assert.Single(results[0]).Id, Assert.Single(results[1]).Id);
        Assert.All(results.SelectMany(x => x), x => Assert.Equal(1, x.Attempts));
    }

    [PostgreSqlFact]
    public async Task Atomicidade_payload_replay_e_constraints()
    {
        await using var env = new OutboxDatabase(); await env.InitializeAsync();
        var pedido = await env.CriarAsync();
        await using var db = env.Open();
        var m = await db.Outbox.AsNoTracking().SingleAsync();
        var e = JsonSerializer.Deserialize<OrderPlacedEvent>(m.Payload)!;
        Assert.Equal((pedido.Id, pedido.UserId, pedido.GameId, pedido.Price, pedido.Currency),
            (e.OrderId, e.UserId, e.GameId, e.Price, e.Currency));
        Assert.Equal(1, e.Version); Assert.Equal(TimeSpan.Zero, e.OccurredAt.Offset);
        var f = new PedidoFakes();
        var h = new ManipuladorCriarPedido(new RepositorioPedidos(db), f, f, new LockUsuarioJogo(db), new RepositorioOutbox(db), f.Correlacao);
        Assert.Equal(pedido.Id, (await h.ExecutarAsync(pedido.UserId, pedido.GameId, pedido.IdempotencyKey)).Pedido!.Id);
        Assert.Equal(1, await db.Outbox.CountAsync());
        Assert.Null(m.PublishedAt); Assert.Equal(0, m.Attempts);
        foreach (var duplicate in new[] { e with { OrderId = Guid.NewGuid() }, e with { EventId = Guid.NewGuid() } })
        {
            db.Outbox.Add(MensagemOutbox.De(duplicate));
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState);
            db.ChangeTracker.Clear();
        }
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE catalog_outbox SET \"Attempts\" = -1"));
    }

    [PostgreSqlFact]
    public async Task Falha_real_de_outbox_e_falha_antes_commit_desfazem_ambos()
    {
        await using var env = new OutboxDatabase(); await env.InitializeAsync();
        await using var db = env.Open();
        var game = new Jogo(Guid.NewGuid(), "Rollback", null, null, 10);
        db.Jogos.Add(game); await db.SaveChangesAsync();
        var f = new PedidoFakes(); f.Jogos.Add(game);
        // A gravação da Outbox efetivamente falha no PostgreSQL, depois do INSERT do pedido.
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE catalog_outbox ADD CONSTRAINT teste_falha CHECK (false)");
        var h = new ManipuladorCriarPedido(new RepositorioPedidos(db), f, f, new LockUsuarioJogo(db), new RepositorioOutbox(db), f.Correlacao);
        await Assert.ThrowsAsync<DbUpdateException>(() => h.ExecutarAsync(Guid.NewGuid(), game.Id, Guid.NewGuid()));
        await using (var verify = env.Open()) { Assert.Empty(await verify.Pedidos.ToListAsync()); Assert.Empty(await verify.Outbox.ToListAsync()); }
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE catalog_outbox DROP CONSTRAINT teste_falha");
        await using (var other = env.Open())
        {
            await using var tx = await other.Database.BeginTransactionAsync();
            var p = new FCG.Catalog.Domain.Orders.Pedido(Guid.NewGuid(), game.Id, 10, Guid.NewGuid());
            await new RepositorioPedidos(other).AdicionarAsync(p);
            await new RepositorioOutbox(other).AdicionarAsync(OrderPlacedEvent.De(p, Guid.NewGuid()));
            // Dispose sem commit simula interrupção depois de ambos SaveChanges.
        }
        await using var final = env.Open(); Assert.Empty(await final.Pedidos.ToListAsync()); Assert.Empty(await final.Outbox.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task Reservas_skip_locked_expiracao_tokens_e_retry_estavel()
    {
        await using var env = new OutboxDatabase(); await env.InitializeAsync();
        await env.CriarAsync(); await env.CriarAsync();
        await using var db = env.Open(); await using var other = env.Open();
        var r = new RepositorioOutbox(db); var r2 = new RepositorioOutbox(other);
        var rows = await db.Outbox.AsNoTracking().OrderBy(x => x.OccurredAt).ToListAsync();
        // Mantém o primeiro registro bloqueado em conexão independente: SKIP LOCKED deve avançar.
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM catalog_outbox WHERE \"Id\" = {rows[0].Id} FOR UPDATE");
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var second = Assert.Single(await r2.ReservarAsync(1, TimeSpan.FromMinutes(1), limit.Token));
            Assert.Equal(rows[1].Id, second.Id);
            await tx.RollbackAsync();
        }
        var first = Assert.Single(await r.ReservarAsync(1, TimeSpan.FromMinutes(1), default));
        Assert.Equal(rows[0].Id, first.Id);
        Assert.Empty(await r2.ReservarAsync(1, TimeSpan.FromMinutes(1), default));
        Assert.False(await r2.PublicadaAsync(first.Id, Guid.NewGuid(), default));
        Assert.False(await r2.FalhouAsync(first.Id, Guid.NewGuid(), TimeSpan.Zero, "wrong", default));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE catalog_outbox SET \"LockedUntil\" = now() - interval '1 second' WHERE \"Id\" = {first.Id}");
        var renewed = Assert.Single(await r2.ReservarAsync(1, TimeSpan.FromMinutes(1), default));
        Assert.NotEqual(first.LockToken, renewed.LockToken); Assert.Equal(2, renewed.Attempts);
        Assert.False(await r.PublicadaAsync(first.Id, first.LockToken!.Value, default));
        Assert.True(await r2.FalhouAsync(renewed.Id, renewed.LockToken!.Value, TimeSpan.Zero, new string('x', 600), default));
        var retry = Assert.Single(await r.ReservarAsync(1, TimeSpan.FromMinutes(1), default));
        Assert.Equal((first.EventId, first.CorrelationId, first.OccurredAt, first.Payload), (retry.EventId, retry.CorrelationId, retry.OccurredAt, retry.Payload));
        Assert.Equal(512, retry.LastError!.Length); Assert.Equal(3, retry.Attempts);
        Assert.True(await r.PublicadaAsync(retry.Id, retry.LockToken!.Value, default));
        Assert.False(await r.PublicadaAsync(retry.Id, retry.LockToken!.Value, default));
    }
}
