using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Catalog.Api.Contracts.Catalog.Jogos;
using FCG.Catalog.Domain.Catalog.Entities;
using Xunit;

namespace FCG.Catalog.IntegrationTests;

public sealed class JogosApiTests
{
    [Fact]
    public async Task Public_reads_preserve_contract_and_pagination()
    {
        using var factory = new CatalogFactory();
        var jogo = new Jogo(Guid.NewGuid(), "Jogo", "Descrição", "12", 15m);
        factory.Repository.Jogos.Add(jogo);
        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/jogos/{jogo.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var obtained = await response.Content.ReadFromJsonAsync<RespostaJogo>();
        Assert.Equal(jogo.Id, obtained!.Id);
        Assert.Equal(jogo.Titulo, obtained.Titulo);
        Assert.Equal(jogo.Preco, obtained.Preco);
        Assert.Equal(jogo.DataCadastro, obtained.DataCadastro);
        var list = await client.GetFromJsonAsync<RespostaListaJogos>("/api/v1/jogos");
        Assert.Single(list!.Itens);
        Assert.Equal(1, list.Pagina);
        Assert.Equal(20, list.TamanhoPagina);
        var page = await client.GetFromJsonAsync<RespostaListaJogos>("/api/v1/jogos?pagina=2&tamanhoPagina=1");
        Assert.Empty(page!.Itens);
        Assert.Equal(2, page.Pagina);
        Assert.Equal(1, page.TamanhoPagina);
    }

    [Theory]
    [InlineData("/api/v1/jogos?pagina=0", 400)]
    [InlineData("/api/v1/jogos?tamanhoPagina=101", 400)]
    [InlineData("/api/v1/jogos/00000000-0000-0000-0000-000000000000", 400)]
    [InlineData("/api/v1/jogos/11111111-1111-1111-1111-111111111111", 404)]
    public async Task Public_read_errors_are_preserved(string path, int expected)
    {
        using var factory = new CatalogFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync(path);
        Assert.Equal(expected, (int)response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expected, problem.GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("POST", null, 401)]
    [InlineData("PUT", null, 401)]
    [InlineData("POST", "Usuario", 403)]
    [InlineData("PUT", "Usuario", 403)]
    [InlineData("POST", "Administrador", 201)]
    [InlineData("PUT", "Administrador", 200)]
    [InlineData("POST", "administrador", 403)]
    [InlineData("PUT", "administrador", 403)]
    public async Task Writes_require_administrator(string method, string? role, int expected)
    {
        using var factory = new CatalogFactory();
        var jogo = new Jogo(Guid.NewGuid(), "Original", null, null, 10m);
        factory.Repository.Jogos.Add(jogo);
        using var client = factory.CreateClient();
        if (role is not null) client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(role: role));
        using var request = new HttpRequestMessage(new HttpMethod(method),
            method == "POST" ? "/api/v1/jogos" : $"/api/v1/jogos/{jogo.Id}")
        {
            Content = JsonContent.Create(new { titulo = "  Novo  ", descricao = " desc ", faixaEtaria = " 12 ", preco = 0m })
        };
        var response = await client.SendAsync(request);
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Equal(expected < 400 ? 1 : 0, factory.Repository.Writes);
        if (expected < 400)
        {
            var body = await response.Content.ReadFromJsonAsync<RespostaJogo>();
            Assert.Equal("Novo", body!.Titulo);
            Assert.Equal("desc", body.Descricao);
            Assert.Equal("12", body.FaixaEtaria);
            Assert.True(body.Ativo);
            Assert.Equal(0m, body.Preco);
            if (method == "POST")
            {
                Assert.Equal($"/api/v1/jogos/{body.Id}", response.Headers.Location!.OriginalString);
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(response.Headers.Location)).StatusCode);
            }
            else
            {
                Assert.Equal(jogo.Id, body.Id);
                Assert.Equal(jogo.DataCadastro, body.DataCadastro);
            }
        }
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task Invalid_data_returns_validation_problem_without_writes(string method)
    {
        using var factory = new CatalogFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token());
        using var request = new HttpRequestMessage(new HttpMethod(method),
            method == "POST" ? "/api/v1/jogos" : $"/api/v1/jogos/{Guid.NewGuid()}")
        { Content = JsonContent.Create(new { titulo = " ", preco = -1 }) };
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("titulo", out _));
        Assert.True(body.GetProperty("errors").TryGetProperty("preco", out _));
        Assert.Equal(0, factory.Repository.Writes);
    }

    [Fact]
    public async Task Update_missing_game_returns_not_found()
    {
        using var factory = new CatalogFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token());
        var response = await client.PutAsJsonAsync($"/api/v1/jogos/{Guid.NewGuid()}", new { titulo = "Jogo", preco = 1 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, factory.Repository.Writes);
    }

    [Fact]
    public async Task Swagger_lists_game_and_order_operations()
    {
        using var factory = new CatalogFactory();
        using var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = document.GetProperty("paths");
        Assert.Equal(4, paths.EnumerateObject().Count());
        Assert.Equal(new[] { "post" }, paths.GetProperty("/api/v1/pedidos").EnumerateObject().Select(p => p.Name));
        Assert.Equal(new[] { "get" }, paths.GetProperty("/api/v1/pedidos/{id}").EnumerateObject().Select(p => p.Name));
        Assert.Equal(new[] { "get", "post" }, paths.GetProperty("/api/v1/jogos").EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal(new[] { "get", "put" }, paths.GetProperty("/api/v1/jogos/{id}").EnumerateObject().Select(p => p.Name).Order().ToArray());
    }
}
