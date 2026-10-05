using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FCG.Catalog.Api.Controllers;
using FCG.Catalog.Application.Orders;
using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Domain.Orders;
using FCG.Catalog.Tests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FCG.Catalog.IntegrationTests;

public sealed class PedidosApiTests
{
    internal static WebApplicationFactory<Program> WithFakes(CatalogFactory tokens, PedidoFakes fake) =>
        tokens.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IRepositorioJogos>();
            s.RemoveAll<IRepositorioPedidos>();
            s.RemoveAll<IRegistroOutbox>();
            s.AddSingleton<IRegistroOutbox>(fake);
            s.RemoveAll<ILockUsuarioJogo>();
            s.AddSingleton<IRepositorioJogos>(fake);
            s.AddSingleton<IRepositorioPedidos>(fake);
            s.AddSingleton<ILockUsuarioJogo>(fake);
            s.RemoveAll<IConsultaBiblioteca>();
            s.AddSingleton<IConsultaBiblioteca>(fake);
        }));

    internal static async Task<HttpResponseMessage> Post(HttpClient client, Guid game, string? key, object? payload = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pedidos")
        { Content = JsonContent.Create(payload ?? new { jogoId = game }) };
        if (key is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Cria_replay_terminais_e_identidade_do_sub()
    {
        using var tokens = new CatalogFactory();
        var fake = new PedidoFakes();
        var game = new Jogo(Guid.NewGuid(), "Game", null, null, 12.34m);
        fake.Jogos.Add(game);
        using var factory = WithFakes(tokens, fake);
        using var client = factory.CreateClient();
        var user = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.Token(role: "Usuario", userId: user));
        var key = Guid.NewGuid().ToString();
        var first = await Post(client, game.Id, key, new { jogoId = game.Id, userId = Guid.NewGuid(), price = 0, currency = "USD", status = "Paid" });
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var body = (await first.Content.ReadFromJsonAsync<RespostaPedido>())!;
        Assert.Equal($"/api/v1/pedidos/{body.OrderId}", first.Headers.Location!.OriginalString);
        Assert.Equal(user, Assert.Single(fake.Pedidos).UserId);
        Assert.Equal(12.34m, body.Price);
        Assert.Equal("BRL", body.Currency);
        Assert.Equal("PendingPayment", body.Status);
        var replay = await Post(client, game.Id, key);
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        Assert.Equal(body.OrderId, (await replay.Content.ReadFromJsonAsync<RespostaPedido>())!.OrderId);
        fake.Pedidos[0].Finalizar(StatusPedido.Paid);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, game.Id, key)).StatusCode);
        var rejected = new Pedido(user, game.Id, 1, Guid.NewGuid());
        rejected.Finalizar(StatusPedido.Rejected);
        fake.Pedidos.Add(rejected);
        var terminal = await Post(client, game.Id, rejected.IdempotencyKey.ToString());
        Assert.Equal(HttpStatusCode.OK, terminal.StatusCode);
        Assert.Equal(rejected.Id, (await terminal.Content.ReadFromJsonAsync<RespostaPedido>())!.OrderId);
    }

    [Theory]
    [InlineData("missing-key", 400)]
    [InlineData("invalid-key", 400)]
    [InlineData("empty-key", 400)]
    [InlineData("no-token", 401)]
    [InlineData("invalid-token", 401)]
    [InlineData("inactive", 409)]
    [InlineData("missing-game", 404)]
    [InlineData("key-reused", 409)]
    [InlineData("pending", 409)]
    [InlineData("owned", 409)]
    public async Task Matriz_post(string scenario, int expected)
    {
        using var tokens = new CatalogFactory();
        var fake = new PedidoFakes();
        var user = Guid.NewGuid();
        var game = new Jogo(Guid.NewGuid(), "Game", null, null, 1);
        if (scenario != "missing-game") fake.Jogos.Add(game);
        if (scenario == "inactive") typeof(Jogo).GetProperty(nameof(Jogo.Ativo))!.SetValue(game, false);
        string? key = Guid.NewGuid().ToString();
        if (scenario == "missing-key") key = null;
        if (scenario == "invalid-key") key = "invalid";
        if (scenario == "empty-key") key = Guid.Empty.ToString();
        if (scenario == "key-reused") fake.Pedidos.Add(new Pedido(user, Guid.NewGuid(), 1, Guid.Parse(key!)));
        if (scenario == "pending") fake.Pedidos.Add(new Pedido(user, game.Id, 1, Guid.NewGuid()));
        fake.Possui = scenario == "owned";
        using var factory = WithFakes(tokens, fake);
        using var client = factory.CreateClient();
        if (scenario != "no-token") client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            tokens.Token(scenario == "invalid-token" ? "bad-signature" : "valid", "Usuario", user));
        Assert.Equal(expected, (int)(await Post(client, game.Id, key)).StatusCode);
    }

    [Theory]
    [InlineData("owner", 200)]
    [InlineData("admin", 200)]
    [InlineData("other", 403)]
    [InlineData("missing", 404)]
    [InlineData("anonymous", 401)]
    [InlineData("invalid", 401)]
    public async Task Matriz_get(string scenario, int expected)
    {
        using var tokens = new CatalogFactory();
        var fake = new PedidoFakes();
        var p = new Pedido(Guid.NewGuid(), Guid.NewGuid(), 9.99m, Guid.NewGuid());
        fake.Pedidos.Add(p);
        using var factory = WithFakes(tokens, fake);
        using var client = factory.CreateClient();
        if (scenario != "anonymous") client.DefaultRequestHeaders.Authorization = new("Bearer",
            tokens.Token(scenario == "invalid" ? "bad-signature" : "valid", scenario == "admin" ? "Administrador" : "Usuario",
                scenario == "owner" ? p.UserId : Guid.NewGuid()));
        var result = await client.GetAsync($"/api/v1/pedidos/{(scenario == "missing" ? Guid.NewGuid() : p.Id)}");
        Assert.Equal(expected, (int)result.StatusCode);
        if (expected == 200)
        {
            Assert.Equal(p.Id, (await result.Content.ReadFromJsonAsync<RespostaPedido>())!.OrderId);
            var json = await result.Content.ReadAsStringAsync();
            Assert.DoesNotContain("userId", json);
            Assert.DoesNotContain("idempotencyKey", json);
        }
    }
}
