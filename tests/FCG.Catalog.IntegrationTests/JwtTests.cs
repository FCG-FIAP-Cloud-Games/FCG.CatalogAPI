using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FCG.Catalog.IntegrationTests;

public sealed class JwtTests
{
    public static IEnumerable<object[]> Tokens()
    {
        string[] valid = ["valid", "second-key", "missing-nbf", "nbf-within-skew", "exp-within-skew"];
        string[] invalid = ["missing-kid", "unknown-kid", "kid-case", "wrong-key", "bad-signature",
            "bad-algorithm", "hmac", "unsigned", "bad-issuer", "bad-audience", "expired", "future-nbf",
            "missing-exp", "missing-sub", "empty-sub", "whitespace-sub", "invalid-sub", "zero-sub", "duplicate-sub"];
        foreach (var method in new[] { "POST", "PUT" })
        {
            foreach (var scenario in valid) yield return [method, scenario, true];
            foreach (var scenario in invalid) yield return [method, scenario, false];
        }
    }

    [Theory]
    [MemberData(nameof(Tokens))]
    public async Task Jwt_is_validated_before_game_operations(string method, string scenario, bool valid)
    {
        using var factory = new CatalogFactory();
        var jogo = new FCG.Catalog.Domain.Catalog.Entities.Jogo(Guid.NewGuid(), "Jogo", null, null, 1);
        factory.Repository.Jogos.Add(jogo);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(scenario));
        using var request = new HttpRequestMessage(new HttpMethod(method),
            method == "POST" ? "/api/v1/jogos" : $"/api/v1/jogos/{jogo.Id}")
        { Content = JsonContent.Create(new { titulo = "Jogo", preco = 1 }) };
        var response = await client.SendAsync(request);
        Assert.Equal(valid ? (method == "POST" ? HttpStatusCode.Created : HttpStatusCode.OK) : HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(valid ? 1 : 0, factory.Repository.Writes);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task Valid_token_without_role_is_forbidden(string method)
    {
        using var factory = new CatalogFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(role: null));
        using var request = new HttpRequestMessage(new HttpMethod(method),
            method == "POST" ? "/api/v1/jogos" : $"/api/v1/jogos/{Guid.NewGuid()}")
        { Content = JsonContent.Create(new { titulo = "Jogo", preco = 1 }) };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
        Assert.Equal(0, factory.Repository.Writes);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task Malformed_token_is_unauthorized(string method)
    {
        using var factory = new CatalogFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-jwt");
        using var request = new HttpRequestMessage(new HttpMethod(method),
            method == "POST" ? "/api/v1/jogos" : $"/api/v1/jogos/{Guid.NewGuid()}")
        { Content = JsonContent.Create(new { titulo = "Jogo", preco = 1 }) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }
}
