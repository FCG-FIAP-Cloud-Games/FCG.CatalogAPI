using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FCG.Catalog.Api.Contracts.Catalog.Jogos;
using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace FCG.Catalog.IntegrationTests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute(string variable = "CATALOG_TEST_CONNECTION")
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)))
            Skip = $"Configure {variable} para executar a validação PostgreSQL.";
    }
}

public sealed class PostgreSqlPersistenceTests
{
    [PostgreSqlFact("ConnectionStrings__CatalogDatabase")]
    public async Task Configured_catalog_database_has_only_expected_schema()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CatalogDatabase");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename", connection);
        await using (var reader = await command.ExecuteReaderAsync())
        {
            var names = new List<string>();
            while (await reader.ReadAsync()) names.Add(reader.GetString(0));
            Assert.Equal(new[] { "__EFMigrationsHistory", "aquisicoes", "catalog_outbox", "jogos", "pedidos", "rel_CategoriaJogo", "tb_Categorias" }, names);
        }
        await using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(connectionString).Options);
        Assert.Equal(4, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }
    [PostgreSqlFact]
    public async Task Migrations_real_http_crud_and_relational_constraints()
    {
        // Only a fresh, uniquely named database is created and later removed.
        var database = "catalog_c15_test_" + Guid.NewGuid().ToString("N");
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
            await using (var db = new CatalogDbContext(options))
            {
                await db.Database.MigrateAsync();
                Assert.Equal(4, (await db.Database.GetAppliedMigrationsAsync()).Count());
                await db.Database.MigrateAsync();
            }

            // Token helper only; its fake-backed host is never started.
            using var tokens = new CatalogFactory();
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:CatalogDatabase", settings.ConnectionString);
                builder.UseSetting("Jwt:PublicKeys:0:Kid", "users-key-1");
                builder.UseSetting("Jwt:PublicKeys:0:PublicKeyPem", tokens.PublicKey);
            });
            using var client = factory.CreateClient();
            using (var scope = factory.Services.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IRepositorioJogos>();
                Assert.Equal("RepositorioJogos", repository.GetType().Name);
            }
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
            var payload = new { titulo = "Alpha", descricao = "Teste PostgreSQL", faixaEtaria = "12", preco = 12.34m };
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/jogos", payload)).StatusCode);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token(role: "Usuario"));
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/jogos", payload)).StatusCode);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token());
            var created = await client.PostAsJsonAsync("/api/v1/jogos", payload);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var jogo = (await created.Content.ReadFromJsonAsync<RespostaJogo>())!;
            var route = $"/api/v1/jogos/{jogo.Id}";
            var update = await client.PutAsJsonAsync(route,
                new { titulo = "Beta", descricao = (string?)null, faixaEtaria = (string?)null, preco = 45.67m });
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            client.DefaultRequestHeaders.Authorization = null;
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync(route, payload)).StatusCode);
            var fetched = (await client.GetFromJsonAsync<RespostaJogo>(route))!;
            Assert.Equal("Beta", fetched.Titulo);
            Assert.Equal(45.67m, fetched.Preco);
            Assert.True(fetched.Ativo);
            // PostgreSQL stores microseconds; JSON creation can retain finer .NET ticks.
            Assert.True(Math.Abs((fetched.DataCadastro - jogo.DataCadastro).Ticks) < 10);
            var list = (await client.GetFromJsonAsync<RespostaListaJogos>("/api/v1/jogos"))!;
            Assert.Equal(jogo.Id, Assert.Single(list.Itens).Id);

            await using var verification = new CatalogDbContext(options);
            Assert.Equal("Beta", (await verification.Jogos.SingleAsync()).Titulo);
            // Separate scopes exercise persisted ordering, paging and detached updates.
            using (var scope = factory.Services.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IRepositorioJogos>();
                await repository.AdicionarAsync(new Jogo(Guid.NewGuid(), "Alpha", null, null, 1));
                Assert.Equal("Alpha", Assert.Single(await repository.ListarAsync(1, 1)).Titulo);
                var detached = Assert.Single(await repository.ListarAsync(2, 1));
                Assert.Equal(jogo.Id, detached.Id);
                detached.AtualizarDados("Beta atualizado", null, null, 50);
                await repository.AtualizarAsync(detached);
            }
            verification.ChangeTracker.Clear();
            Assert.Equal(50, (await verification.Jogos.SingleAsync(j => j.Id == jogo.Id)).Preco);
            var categoria = new Categoria(Guid.NewGuid(), "Aventura");
            verification.Categorias.Add(categoria);
            verification.CategoriasJogos.Add(new CategoriaJogo
                { Id = Guid.NewGuid(), JogoId = jogo.Id, CategoriaId = categoria.Id });
            await verification.SaveChangesAsync();
            verification.ChangeTracker.Clear();
            verification.CategoriasJogos.Add(new CategoriaJogo
                { Id = Guid.NewGuid(), JogoId = jogo.Id, CategoriaId = categoria.Id });
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => verification.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
            verification.ChangeTracker.Clear();
            verification.CategoriasJogos.Add(new CategoriaJogo
                { Id = Guid.NewGuid(), JogoId = Guid.NewGuid(), CategoriaId = categoria.Id });
            var foreignKey = await Assert.ThrowsAsync<DbUpdateException>(() => verification.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(foreignKey.InnerException).SqlState);
            verification.ChangeTracker.Clear();
            verification.Jogos.Add(new Jogo(Guid.NewGuid(), new string('x', 151), null, null, 1));
            var length = await Assert.ThrowsAsync<DbUpdateException>(() => verification.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, Assert.IsType<PostgresException>(length.InnerException).SqlState);
            verification.ChangeTracker.Clear();
            verification.Categorias.Remove(await verification.Categorias.SingleAsync());
            await verification.SaveChangesAsync();
            Assert.Empty(await verification.CategoriasJogos.ToListAsync());
            await using var connection = new NpgsqlConnection(settings.ConnectionString);
            await connection.OpenAsync();
            await using var tables = new NpgsqlCommand(
                "SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename", connection);
            await using var reader = await tables.ExecuteReaderAsync();
            var names = new List<string>();
            while (await reader.ReadAsync()) names.Add(reader.GetString(0));
            Assert.Equal(new[] { "__EFMigrationsHistory", "aquisicoes", "catalog_outbox", "jogos", "pedidos", "rel_CategoriaJogo", "tb_Categorias" }, names);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
