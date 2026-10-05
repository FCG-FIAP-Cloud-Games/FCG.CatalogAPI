using FCG.Catalog.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FCG.Catalog.IntegrationTests;

internal sealed class BibliotecaDatabase : IAsyncDisposable
{
    private readonly string name = "catalog_c18_test_" + Guid.NewGuid().ToString("N");
    private readonly NpgsqlConnection admin;
    private bool created;
    public string ConnectionString { get; }
    public BibliotecaDatabase()
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CATALOG_TEST_CONNECTION"));
        admin = new NpgsqlConnection(settings.ConnectionString);
        settings.Database = name; settings.Pooling = false;
        ConnectionString = settings.ConnectionString;
    }
    public async Task InitializeAsync()
    {
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin);
        await create.ExecuteNonQueryAsync(); created = true;
        await using var db = Open(); await db.Database.MigrateAsync();
    }
    public CatalogDbContext Open() => new(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(ConnectionString).Options);
    public async ValueTask DisposeAsync()
    {
        if (created)
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
        await admin.DisposeAsync();
    }
}
