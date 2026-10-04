using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FCG.Catalog.IntegrationTests;

public sealed class HostTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Health_is_available_without_public_key(string environment)
    {
        using var factory = CreateFactory(environment);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Development", "/swagger/index.html", HttpStatusCode.OK)]
    [InlineData("Development", "/swagger/v1/swagger.json", HttpStatusCode.OK)]
    [InlineData("Production", "/swagger/index.html", HttpStatusCode.NotFound)]
    [InlineData("Production", "/swagger/v1/swagger.json", HttpStatusCode.NotFound)]
    public async Task Swagger_is_only_available_in_development(
        string environment, string path, HttpStatusCode expected)
    {
        using var factory = CreateFactory(environment);
        using var client = factory.CreateClient();

        Assert.Equal(expected, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Missing_route_returns_problem_details()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/missing-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Protected_controller_rejects_requests_without_configured_key(bool sendToken)
    {
        using var rsa = RSA.Create(2048);
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        if (sendToken)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(rsa));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/test/auth")).StatusCode);
    }

    [Theory]
    [InlineData("FIAP.CloudGames", "FIAP.CloudGames.Api", HttpStatusCode.OK)]
    [InlineData("another-issuer", "FIAP.CloudGames.Api", HttpStatusCode.Unauthorized)]
    [InlineData("FIAP.CloudGames", "another-audience", HttpStatusCode.Unauthorized)]
    public async Task External_public_key_validates_local_rs256_token(
        string issuer, string audience, HttpStatusCode expected)
    {
        // Chave efêmera somente em memória; nenhum material de chave é gravado.
        using var rsa = RSA.Create(2048);
        using var factory = CreateFactory(publicKey: rsa.ExportSubjectPublicKeyInfoPem());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(rsa, issuer, audience));

        Assert.Equal(expected, (await client.GetAsync("/test/auth")).StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment = "Production", string publicKey = "") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            if (!string.IsNullOrEmpty(publicKey))
            {
                builder.UseSetting("Jwt:PublicKeys:0:Kid", "users-key-1");
                builder.UseSetting("Jwt:PublicKeys:0:PublicKeyPem", publicKey);
            }
            builder.UseSetting("Jwt:Issuer", "FIAP.CloudGames");
            builder.UseSetting("Jwt:Audience", "FIAP.CloudGames.Api");
            builder.ConfigureServices(services =>
                services.AddControllers().AddApplicationPart(typeof(AuthProbeController).Assembly));
        });

    private static string CreateToken(RSA rsa, string issuer = "FIAP.CloudGames", string audience = "FIAP.CloudGames.Api") =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: [new System.Security.Claims.Claim("sub", Guid.NewGuid().ToString())],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = "users-key-1" }, SecurityAlgorithms.RsaSha256)));
}

// Controller exclusivo do assembly de testes, não integra a API publicada.
[ApiController]
[Route("test/auth")]
[Authorize]
public sealed class AuthProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}
