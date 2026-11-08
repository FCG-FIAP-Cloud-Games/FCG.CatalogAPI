using System.Net;
using FCG.Catalog.Api.Initialization;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Npgsql;
using Xunit;

namespace FCG.Catalog.IntegrationTests;

public sealed class LocalDemonstrationTests
{
    [Theory]
    [InlineData(null, "Development", false)]
    [InlineData(null, "Production", false)]
    [InlineData("false", "Development", false)]
    [InlineData("false", "Production", false)]
    [InlineData("true", "Development", true)]
    public void Automatic_migrations_require_explicit_local_configuration(string? value, string environment, bool expected)
    {
        var configuration = Configuration(value);
        Assert.Equal(expected, LocalDemonstrationExtensions.IsEnabled(configuration, new TestEnvironment(environment)));
    }

    [Theory]
    [InlineData("yes", "Development")]
    [InlineData("", "Development")]
    [InlineData("true", "Production")]
    [InlineData("true", "Staging")]
    public void Unsafe_or_malformed_demo_configuration_fails(string value, string environment) =>
        Assert.Throws<InvalidOperationException>(() =>
            LocalDemonstrationExtensions.IsEnabled(Configuration(value), new TestEnvironment(environment)));

    [PostgreSqlFact]
    public async Task Startup_migrates_empty_database_and_restart_preserves_data()
    {
        var name = "catalog_startup_test_" + Guid.NewGuid().ToString("N");
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CATALOG_TEST_CONNECTION"));
        await using var admin = new NpgsqlConnection(settings.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin))
            await create.ExecuteNonQueryAsync();
        settings.Database = name;
        settings.Pooling = false;
        try
        {
            for (var run = 0; run < 2; run++)
            {
                using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Development");
                    builder.UseSetting("ConnectionStrings:CatalogDatabase", settings.ConnectionString);
                    builder.UseSetting("DemonstracaoLocal:Habilitada", "true");
                    builder.UseSetting("Outbox:Enabled", "false");
                    builder.UseSetting("RabbitMq:ConsumerEnabled", "false");
                });
                using var client = factory.CreateClient();
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
                await using var database = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
                    .UseNpgsql(settings.ConnectionString).Options);
                Assert.Equal(5, (await database.Database.GetAppliedMigrationsAsync()).Count());
                Assert.Empty(await database.Database.GetPendingMigrationsAsync());
                if (run == 0)
                    await database.Database.ExecuteSqlRawAsync(
                        "INSERT INTO jogos(id,titulo,preco,ativo,data_cadastro) VALUES('13f11b88-876e-4526-8d1e-d979a2a9a752','Preservado',49.90,true,now())");
                else
                    Assert.Equal("Preservado", (await database.Jogos.SingleAsync()).Titulo);
            }
        }
        finally
        {
            // Somente o banco descartável com identificador exclusivo deste teste é removido.
            await using var drop = new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static IConfiguration Configuration(string? value) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["DemonstracaoLocal:Habilitada"] = value }).Build();

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "CatalogTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
