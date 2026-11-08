using FCG.Catalog.Application.Orders;
using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Domain.Orders;
using FCG.Catalog.Infrastructure.Data.EF;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using FCG.Catalog.Infrastructure.Outbox;
using FCG.Catalog.Infrastructure.Repositories;
using FCG.Catalog.Tests;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace FCG.Catalog.IntegrationTests;

internal sealed class OutboxDatabase : IAsyncDisposable
{
    private readonly string name = "catalog_c17_test_" + Guid.NewGuid().ToString("N");
    private readonly NpgsqlConnection admin;
    public DbContextOptions<CatalogDbContext> Options { get; }
    public OutboxDatabase()
    {
        var b = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CATALOG_TEST_CONNECTION"));
        admin = new NpgsqlConnection(b.ConnectionString);
        b.Database = name; b.Pooling = false;
        Options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(b.ConnectionString).Options;
    }
    public async Task InitializeAsync()
    {
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin);
        await create.ExecuteNonQueryAsync();
        await using var db = Open(); await db.Database.MigrateAsync();
    }
    public CatalogDbContext Open() => new(Options);
    public async Task<Pedido> CriarAsync()
    {
        await using var db = Open();
        var game = new Jogo(Guid.NewGuid(), "Outbox", null, null, 42.50m);
        db.Jogos.Add(game); await db.SaveChangesAsync();
        var f = new PedidoFakes(); f.Jogos.Add(game);
        var h = new ManipuladorCriarPedido(new RepositorioPedidos(db), f, f,
            new LockUsuarioJogo(db), new RepositorioOutbox(db), f.Correlacao);
        return (await h.ExecutarAsync(Guid.NewGuid(), game.Id, Guid.NewGuid())).Pedido!;
    }
    public async ValueTask DisposeAsync()
    {
        if (admin.State == System.Data.ConnectionState.Open)
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {name} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
        await admin.DisposeAsync();
    }
}
