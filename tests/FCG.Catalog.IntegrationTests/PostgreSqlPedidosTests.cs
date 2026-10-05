using System.Net;
using System.Net.Http.Json;
using FCG.Catalog.Api.Controllers;
using FCG.Catalog.Application.Orders;
using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Domain.Orders;
using FCG.Catalog.Infrastructure.Data.EF;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace FCG.Catalog.IntegrationTests;

public sealed class PostgreSqlPedidosTests
{
    [PostgreSqlFact("ConnectionStrings__CatalogDatabase")]
    public async Task Schema_autorizado_tem_indices_e_apenas_fk_local()
    {
        await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__CatalogDatabase"));
        await connection.OpenAsync();
        await using var indexes = new NpgsqlCommand("SELECT indexname, indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'pedidos'", connection);
        var definitions = new Dictionary<string, string>();
        await using (var reader = await indexes.ExecuteReaderAsync())
            while (await reader.ReadAsync()) definitions.Add(reader.GetString(0), reader.GetString(1));
        Assert.Contains("UNIQUE INDEX", definitions["ux_pedidos_usuario_chave"]);
        Assert.Contains("(\"UserId\", \"IdempotencyKey\")", definitions["ux_pedidos_usuario_chave"]);
        Assert.Contains("UNIQUE INDEX", definitions["ux_pedidos_usuario_jogo_pendente"]);
        Assert.Contains("(\"UserId\", \"GameId\")", definitions["ux_pedidos_usuario_jogo_pendente"]);
        Assert.Contains("WHERE", definitions["ux_pedidos_usuario_jogo_pendente"]);
        Assert.Contains("'PendingPayment'", definitions["ux_pedidos_usuario_jogo_pendente"]);
        await using var foreignKeys = new NpgsqlCommand("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid = 'public.pedidos'::regclass AND contype = 'f'", connection);
        var constraints = new List<string>();
        await using (var reader = await foreignKeys.ExecuteReaderAsync())
            while (await reader.ReadAsync()) constraints.Add(reader.GetString(0));
        Assert.Equal("FOREIGN KEY (\"GameId\") REFERENCES jogos(id) ON DELETE RESTRICT", Assert.Single(constraints));
    }

    [PostgreSqlFact]
    public async Task Concorrencia_http_indices_fk_e_lock_transacional_reais()
    {
        var database = "catalog_c16_test_" + Guid.NewGuid().ToString("N");
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CATALOG_TEST_CONNECTION"));
        await using var admin = new NpgsqlConnection(settings.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            settings.Database = database;
            settings.Pooling = false;
            var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(settings.ConnectionString).Options;
            await using var db = new CatalogDbContext(options);
            await db.Database.MigrateAsync();
            using var tokens = new CatalogFactory();

            foreach (var scenario in new[] { "same-key", "different-keys", "different-users", "different-games" })
            {
                var game = new Jogo(Guid.NewGuid(), scenario, null, null, 42.50m);
                var otherGame = new Jogo(Guid.NewGuid(), "other", null, null, 12);
                db.Jogos.AddRange(game, otherGame);
                await db.SaveChangesAsync();
                var user = Guid.NewGuid();
                var otherUser = scenario == "different-users" ? Guid.NewGuid() : user;
                var key = Guid.NewGuid();
                var otherKey = scenario == "different-keys" ? Guid.NewGuid() : key;
                var otherGameId = scenario == "different-games" ? otherGame.Id : game.Id;
                var attempts = new TwoCalls();
                var library = new TestLibrary(scenario == "different-games");
                using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Development");
                    builder.UseSetting("Outbox:Enabled", "false");
                    builder.UseSetting("ConnectionStrings:CatalogDatabase", settings.ConnectionString);
                    builder.UseSetting("Jwt:PublicKeys:0:Kid", "users-key-1");
                    builder.UseSetting("Jwt:PublicKeys:0:PublicKeyPem", tokens.PublicKey);
                    builder.ConfigureServices(s =>
                    {
                        s.AddSingleton<IConsultaBiblioteca>(library);
                        s.AddScoped<ILockUsuarioJogo>(p => new ObservedLock(new LockUsuarioJogo(p.GetRequiredService<CatalogDbContext>()), attempts));
                    });
                });
                using var firstClient = factory.CreateClient();
                using var secondClient = factory.CreateClient();
                firstClient.DefaultRequestHeaders.Authorization = new("Bearer", tokens.Token(role: "Usuario", userId: user));
                secondClient.DefaultRequestHeaders.Authorization = new("Bearer", tokens.Token(role: "Usuario", userId: otherUser));

                // Segura o mesmo lock em uma terceira conexão. As duas requisições
                // atingem AcquireAsync antes da liberação, sem depender de Task.Delay.
                await using var blocker = new CatalogDbContext(options);
                await using var transaction = await blocker.Database.BeginTransactionAsync();
                await new LockUsuarioJogo(blocker).AcquireAsync(user, game.Id);
                var firstTask = PedidosApiTests.Post(firstClient, game.Id, key.ToString());
                var secondTask = PedidosApiTests.Post(secondClient, otherGameId, otherKey.ToString());
                await attempts.Ready.Task.WaitAsync(TimeSpan.FromSeconds(20));
                Assert.False(firstTask.IsCompleted);
                await transaction.RollbackAsync();
                var responses = await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(30));
                Assert.All(responses, r => Assert.NotEqual(HttpStatusCode.InternalServerError, r.StatusCode));
                if (scenario is "different-keys" or "different-games")
                {
                    Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Accepted);
                    Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
                    Assert.Equal(1, await db.Pedidos.CountAsync(p => p.UserId == user));
                }
                else
                {
                    Assert.All(responses, r => Assert.Equal(HttpStatusCode.Accepted, r.StatusCode));
                    var a = (await responses[0].Content.ReadFromJsonAsync<RespostaPedido>())!;
                    var b = (await responses[1].Content.ReadFromJsonAsync<RespostaPedido>())!;
                    if (scenario == "same-key")
                    {
                        Assert.Equal(a.OrderId, b.OrderId);
                        Assert.Equal(1, await db.Pedidos.CountAsync(p => p.UserId == user));
                    }
                    else
                    {
                        Assert.NotEqual(a.OrderId, b.OrderId);
                        Assert.Equal(2, await db.Pedidos.CountAsync(p => p.GameId == game.Id));
                    }
                }
                var orderIds = await db.Pedidos.Where(p => p.UserId == user || p.UserId == otherUser).Select(p => p.Id).ToListAsync();
                Assert.Equal(orderIds.Count, await db.Outbox.CountAsync(m => orderIds.Contains(m.OrderId)));
                foreach (var response in responses) response.Dispose();
            }

            // Constraints exercitadas diretamente, sem validações de Application.
            db.ChangeTracker.Clear();
            var existing = await db.Pedidos.FirstAsync();
            await AssertConstraint(db, new Pedido(existing.UserId, existing.GameId, 1, Guid.NewGuid()),
                PostgresErrorCodes.UniqueViolation, "ux_pedidos_usuario_jogo_pendente");
            var extraGame = new Jogo(Guid.NewGuid(), "Extra", null, null, 1);
            db.Jogos.Add(extraGame);
            await db.SaveChangesAsync();
            await AssertConstraint(db, new Pedido(existing.UserId, extraGame.Id, 1, existing.IdempotencyKey),
                PostgresErrorCodes.UniqueViolation, "ux_pedidos_usuario_chave");
            await AssertConstraint(db, new Pedido(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid()),
                PostgresErrorCodes.ForeignKeyViolation, "FK_pedidos_jogos_GameId");
            var tracked = await db.Pedidos.SingleAsync(p => p.Id == existing.Id);
            tracked.Finalizar(StatusPedido.Rejected);
            await db.SaveChangesAsync();
            db.Pedidos.Add(new Pedido(existing.UserId, existing.GameId, 1, Guid.NewGuid()));
            await db.SaveChangesAsync(); // O índice parcial permite nova compra após estado terminal.
            await Assert.ThrowsAsync<InvalidOperationException>(() => new LockUsuarioJogo(db).AcquireAsync(existing.UserId, existing.GameId));

            // O lock também se libera por commit.
            await using var holder = new CatalogDbContext(options);
            await using (var transaction = await holder.Database.BeginTransactionAsync())
            {
                await new LockUsuarioJogo(holder).AcquireAsync(existing.UserId, existing.GameId);
                await transaction.CommitAsync();
            }
            await using var next = await db.Database.BeginTransactionAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await new LockUsuarioJogo(db).AcquireAsync(existing.UserId, existing.GameId, timeout.Token);
            await next.RollbackAsync();
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task AssertConstraint(CatalogDbContext db, Pedido p, string code, string name)
    {
        db.Pedidos.Add(p);
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(code, pg.SqlState);
        Assert.Equal(name, pg.ConstraintName);
        db.ChangeTracker.Clear();
    }

    private sealed class TwoCalls
    {
        private int count;
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Signal() { if (Interlocked.Increment(ref count) == 2) Ready.TrySetResult(); }
    }
    private sealed class ObservedLock(ILockUsuarioJogo inner, TwoCalls calls) : ILockUsuarioJogo
    {
        public Task AcquireAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
        {
            calls.Signal();
            return inner.AcquireAsync(userId, gameId, cancellationToken);
        }
    }
    // Exclusivo do host de testes. C18 é a única fonte futura de posse em produção.
    private sealed class TestLibrary(bool synchronize) : IConsultaBiblioteca
    {
        private readonly TwoCalls calls = new();
        public async Task<bool> PossuiJogoAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
        {
            if (synchronize)
            {
                calls.Signal();
                await calls.Ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return false;
        }
    }
}

