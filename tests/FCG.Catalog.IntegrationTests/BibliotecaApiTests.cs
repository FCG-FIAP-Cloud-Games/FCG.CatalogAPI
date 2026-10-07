using System.Net;
using System.Net.Http.Json;
using FCG.Catalog.Api.Controllers;
using FCG.Catalog.Application.Library;
using FCG.Catalog.Application.Orders;
using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Domain.Library;
using FCG.Catalog.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FCG.Catalog.IntegrationTests;

public sealed class BibliotecaApiTests
{
    [Theory]
    [InlineData("anonymous")]
    [InlineData("bad-signature")]
    [InlineData("missing-sub")]
    [InlineData("invalid-sub")]
    [InlineData("zero-sub")]
    [InlineData("duplicate-sub")]
    public async Task Exige_token_e_sub_validos(string scenario)
    {
        using var factory = new CatalogFactory(); using var client = factory.CreateClient();
        if (scenario != "anonymous") client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(scenario));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/biblioteca")).StatusCode);
    }

    private static WebApplicationFactory<Program> RealFactory(BibliotecaDatabase env, CatalogFactory tokens) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("Outbox:Enabled", "false");
            b.UseSetting("RabbitMq:ConsumerEnabled", "false");
            b.UseSetting("ConnectionStrings:CatalogDatabase", env.ConnectionString);
            b.UseSetting("Jwt:Issuer", "FIAP.CloudGames"); b.UseSetting("Jwt:Audience", "FIAP.CloudGames.Api");
            b.UseSetting("Jwt:PublicKeys:0:Kid", "users-key-1");
            b.UseSetting("Jwt:PublicKeys:0:PublicKeyPem", tokens.PublicKey);
        });

    [PostgreSqlFact]
    public async Task Biblioteca_real_isola_sub_admin_ordenacao_inativos_e_tentativas_de_trocar_titular()
    {
        await using var env = new BibliotecaDatabase(); await env.InitializeAsync();
        var user = Guid.NewGuid(); var other = Guid.NewGuid();
        var games = Enumerable.Range(0, 4).Select(i => new Jogo(Guid.NewGuid(), $"Game {i}", null, null, 10)).ToArray();
        var date = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);
        await using (var db = env.Open())
        {
            typeof(Jogo).GetProperty(nameof(Jogo.Ativo))!.SetValue(games[0], false);
            db.Jogos.AddRange(games);
            db.Aquisicoes.AddRange(
                Aquisicao.Historica(Guid.Parse("00000000-0000-0000-0000-000000000001"), user, games[0].Id, date),
                Aquisicao.Historica(Guid.Parse("00000000-0000-0000-0000-000000000002"), user, games[1].Id, date),
                Aquisicao.Historica(Guid.NewGuid(), user, games[2].Id, date.AddDays(-1)),
                Aquisicao.Historica(Guid.NewGuid(), other, games[3].Id, date.AddDays(1)));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var query = new ConsultaBiblioteca(db);
            Assert.True(await query.PossuiJogoAsync(user, games[0].Id));
            Assert.False(await query.PossuiJogoAsync(user, games[3].Id));
            Assert.Equal(3, (await query.ListarAsync(user)).Count);
            Assert.Empty(db.ChangeTracker.Entries());
        }
        using var tokens = new CatalogFactory(); using var factory = RealFactory(env, tokens);
        using var client = factory.CreateClient();
        foreach (var role in new[] { "Usuario", "Administrador" })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.Token(role: role, userId: user));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/biblioteca?userId={other}")
            { Content = JsonContent.Create(new { userId = other }) };
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var items = (await response.Content.ReadFromJsonAsync<ItemBiblioteca[]>())!;
            Assert.Equal(games.Take(3).Select(g => g.Id), items.Select(i => i.GameId));
            Assert.Equal("Game 0", items[0].Title); Assert.Equal(date, items[0].AcquiredAt);
            using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(new[] { "acquiredAt", "gameId", "title" }, json.RootElement[0].EnumerateObject().Select(p => p.Name).OrderBy(n => n));
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.Token(userId: other));
        Assert.Equal(games[3].Id, Assert.Single((await client.GetFromJsonAsync<ItemBiblioteca[]>("/api/v1/biblioteca"))!).GameId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.Token(userId: Guid.NewGuid()));
        Assert.Empty((await client.GetFromJsonAsync<ItemBiblioteca[]>("/api/v1/biblioteca"))!);
    }

    [PostgreSqlFact]
    public async Task Post_real_sem_503_posse_409_compra_202_e_replay_preservado()
    {
        await using var env = new BibliotecaDatabase(); await env.InitializeAsync();
        await using var db = env.Open(); var order = await PostgreSqlBibliotecaTests.Seed(db);
        using var tokens = new CatalogFactory(); using var factory = RealFactory(env, tokens);
        using var scope = factory.Services.CreateScope();
        Assert.IsType<ConsultaBiblioteca>(scope.ServiceProvider.GetRequiredService<IConsultaBiblioteca>());
        using var client = factory.CreateClient();
        var user = Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.Token(role: "Usuario", userId: user));
        using var first = await PedidosApiTests.Post(client, order.GameId, key);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var body = (await first.Content.ReadFromJsonAsync<RespostaPedido>())!;
        db.Aquisicoes.Add(Aquisicao.Historica(Guid.NewGuid(), user, order.GameId, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        using var owned = await PedidosApiTests.Post(client, order.GameId, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Conflict, owned.StatusCode);
        Assert.Contains("já possui", (await owned.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>())!.Detail);
        using var replay = await PedidosApiTests.Post(client, order.GameId, key);
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        Assert.Equal(body.OrderId, (await replay.Content.ReadFromJsonAsync<RespostaPedido>())!.OrderId);
        Assert.Equal(1, await db.Outbox.CountAsync(x => x.OrderId == body.OrderId));
    }
}
