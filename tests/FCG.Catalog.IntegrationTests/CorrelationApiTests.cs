using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Tests;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
namespace FCG.Catalog.IntegrationTests;

public sealed class CorrelationApiTests
{
    [Fact]
    public async Task Worker_habilitado_sem_banco_ou_broker_nao_impede_health_e_swagger()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("Outbox:Enabled", "true");
            b.UseSetting("RabbitMq:ConsumerEnabled", "true");
            b.UseSetting("RabbitMq:Host", "127.0.0.1");
            b.UseSetting("RabbitMq:Port", "1");
            b.UseSetting("ConnectionStrings:CatalogDatabase", "");
            b.UseSetting("RabbitMq:Username", "");
            b.UseSetting("RabbitMq:Password", "");
        });
        using var client = factory.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }

    [Theory]
    [InlineData("valid")] [InlineData("absent")] [InlineData("invalid")] [InlineData("multiple")] [InlineData("empty")]
    public async Task Header_e_evento_compartilham_correlacao_sem_alterar_replay(string scenario)
    {
        using var tokens = new CatalogFactory(); var fake = new PedidoFakes();
        var game = new Jogo(Guid.NewGuid(), "Correlation", null, null, 1); fake.Jogos.Add(game);
        using var factory = PedidosApiTests.WithFakes(tokens, fake); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.Token(role: "Usuario"));
        var input = Guid.NewGuid();
        var headers = scenario switch { "valid" => new[] { input.ToString() }, "absent" => [],
            "invalid" => ["bad"], "multiple" => [input.ToString(), Guid.NewGuid().ToString()], _ => [Guid.Empty.ToString()] };
        if (headers.Length > 0) client.DefaultRequestHeaders.TryAddWithoutValidation("X-Correlation-ID", headers);
        var key = Guid.NewGuid().ToString();
        using var response = await PedidosApiTests.Post(client, game.Id, key);
        Assert.Equal(202, (int)response.StatusCode);
        var correlation = Guid.Parse(Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        Assert.NotEqual(Guid.Empty, correlation);
        Assert.Equal(correlation, Assert.Single(fake.Eventos).CorrelationId);
        if (scenario == "valid") Assert.Equal(input, correlation); else Assert.NotEqual(input, correlation);
        client.DefaultRequestHeaders.Remove("X-Correlation-ID");
        client.DefaultRequestHeaders.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        using var replay = await PedidosApiTests.Post(client, game.Id, key);
        Assert.Equal(202, (int)replay.StatusCode);
        Assert.Equal(correlation, Assert.Single(fake.Eventos).CorrelationId);
    }
}
