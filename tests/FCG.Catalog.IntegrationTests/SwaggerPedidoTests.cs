using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace FCG.Catalog.IntegrationTests;

public sealed class SwaggerPedidoTests
{
    [Fact]
    public async Task Documento_publico_preserva_header_e_UI_registra_plugin_isolado()
    {
        using var factory = new CatalogFactory();
        using var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var paths = doc.RootElement.GetProperty("paths");
        var parameter = Assert.Single(paths.GetProperty("/api/v1/pedidos").GetProperty("post")
            .GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "Idempotency-Key");
        Assert.Equal("header", parameter.GetProperty("in").GetString());
        Assert.True(parameter.GetProperty("required").GetBoolean());
        Assert.Equal("string", parameter.GetProperty("schema").GetProperty("type").GetString());
        Assert.Equal("uuid", parameter.GetProperty("schema").GetProperty("format").GetString());
        Assert.True(Guid.TryParse(parameter.GetProperty("example").GetString(), out var example));
        Assert.NotEqual(Guid.Empty, example);
        Assert.False(parameter.GetProperty("schema").TryGetProperty("default", out _));
        foreach (var path in paths.EnumerateObject())
        foreach (var operation in path.Value.EnumerateObject())
        {
            if (path.Name == "/api/v1/pedidos" && operation.Name == "post") continue;
            if (operation.Value.TryGetProperty("parameters", out var parameters))
                Assert.DoesNotContain(parameters.EnumerateArray(), p => p.GetProperty("name").GetString() == "Idempotency-Key");
        }
        Assert.Contains("./pedido-intencao.js", await client.GetStringAsync("/swagger/index.html"));
        var bootstrap = await client.GetStringAsync("/swagger/index.js");
        Assert.Contains("PedidoIntencaoPlugin", bootstrap);
        Assert.Contains("window.PedidoIntencao.intercept", bootstrap);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/pedido-intencao.js")).StatusCode);
    }

    [Fact]
    public async Task Script_de_demonstracao_nao_e_servido_em_producao()
    {
        using var original = new CatalogFactory();
        using var factory = original.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/pedido-intencao.js")).StatusCode);
    }
}
