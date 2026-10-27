using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using FCG.Catalog.Api.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FCG.Catalog.IntegrationTests;

public sealed class JwksTests
{
    [Fact]
    public async Task Cached_key_validates_tokens_and_preserves_issuer_audience_and_algorithm_checks()
    {
        using var rsa = RSA.Create(2048);
        using var handler = new Sender(Payload(rsa, "users-key"));
        using var client = new HttpClient(handler);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new DevelopmentEnvironment());
        services.AddCatalogAuthentication(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:JwksUri"] = "http://usersapi:8080/.well-known/jwks.json"
        }).Build());
        services.AddSingleton<IHttpClientFactory>(new ClientFactory(client));
        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<JwksKeyCache>();
        Assert.True(await cache.RefreshAsync(CancellationToken.None));
        var parameters = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("Bearer").TokenValidationParameters;
        var validator = new JwtSecurityTokenHandler();
        validator.ValidateToken(Token(rsa), parameters, out _);
        Assert.ThrowsAny<SecurityTokenException>(() => validator.ValidateToken(Token(rsa, issuer: "other"), parameters, out _));
        Assert.ThrowsAny<SecurityTokenException>(() => validator.ValidateToken(Token(rsa, audience: "other"), parameters, out _));
        Assert.ThrowsAny<SecurityTokenException>(() => validator.ValidateToken(Token(rsa, algorithm: SecurityAlgorithms.RsaSha512), parameters, out _));
        Assert.ThrowsAny<SecurityTokenException>(() => validator.ValidateToken(Token(rsa, kid: "unknown"), parameters, out _));
        Assert.ThrowsAny<SecurityTokenException>(() => validator.ValidateToken(Token(rsa, expires: DateTime.UtcNow.AddMinutes(-2)), parameters, out _));
        Assert.Equal(1, handler.Requests); // A validação não consulta a rede por operação.
    }

    [Fact]
    public async Task Refresh_supports_rotation_and_bounded_cache_during_outage()
    {
        using var first = RSA.Create(2048); using var second = RSA.Create(2048);
        using var handler = new Sender(Payload(first, "first")); using var client = new HttpClient(handler);
        var clock = new Clock();
        using var cache = new JwksKeyCache(Options(), new ClientFactory(client), NullLogger<JwksKeyCache>.Instance, clock, true);
        Assert.True(await cache.RefreshAsync(CancellationToken.None));
        Assert.Single(cache.Resolve("first"));
        handler.Document = Payload(second, "second");
        Assert.True(await cache.RefreshAsync(CancellationToken.None));
        Assert.Empty(cache.Resolve("first")); Assert.Single(cache.Resolve("second"));
        handler.Unavailable = true;
        Assert.False(await cache.RefreshAsync(CancellationToken.None));
        Assert.Single(cache.Resolve("second"));
        clock.Now = clock.Now.AddSeconds(901);
        Assert.False(cache.HasUsableKeys); Assert.Empty(cache.Resolve("second"));
    }

    [Theory]
    [InlineData("private")]
    [InlineData("duplicate")]
    [InlineData("weak")]
    [InlineData("malformed")]
    public async Task Invalid_key_sets_do_not_become_trusted(string scenario)
    {
        using var rsa = RSA.Create(scenario == "weak" ? 1024 : 2048);
        var document = Payload(rsa, "key");
        if (scenario == "private") document = document.Replace("\"kty\":\"RSA\"", "\"kty\":\"RSA\",\"d\":\"private\"");
        if (scenario == "duplicate")
        {
            using var json = JsonDocument.Parse(document);
            var item = json.RootElement.GetProperty("keys")[0].GetRawText();
            document = "{\"keys\":[" + item + "," + item + "]}";
        }
        if (scenario == "malformed") document = "not-json";
        using var handler = new Sender(document); using var client = new HttpClient(handler);
        using var cache = new JwksKeyCache(Options(), new ClientFactory(client), NullLogger<JwksKeyCache>.Instance, new Clock(), true);
        Assert.False(await cache.RefreshAsync(CancellationToken.None));
        Assert.False(cache.HasUsableKeys); Assert.Empty(cache.Resolve("key"));
    }

    [Fact]
    public void Configuration_rejects_insecure_production_and_mixed_key_sources()
    {
        using var client = new HttpClient();
        Assert.Throws<InvalidOperationException>(() => new JwksKeyCache(Options(), new ClientFactory(client), NullLogger<JwksKeyCache>.Instance, new Clock(), false));
        Assert.Throws<InvalidOperationException>(() => JwksKeyCache.ValidateOptions(new JwtOptions
        {
            JwksUri = "https://usersapi/jwks", PublicKeys = [new JwtPublicKeyOptions { Kid = "other" }]
        }));
    }

    private static JwtOptions Options() => new() { JwksUri = "http://usersapi:8080/.well-known/jwks.json" };
    private static string Payload(RSA rsa, string kid)
    {
        var p = rsa.ExportParameters(false);
        return JsonSerializer.Serialize(new { keys = new[] { new { kty = "RSA", use = "sig", alg = "RS256", kid,
            n = Base64UrlEncoder.Encode(p.Modulus!), e = Base64UrlEncoder.Encode(p.Exponent!) } } });
    }
    private static string Token(RSA rsa, string issuer = "FIAP.CloudGames", string audience = "FIAP.CloudGames.Api",
        string kid = "users-key", string algorithm = SecurityAlgorithms.RsaSha256, DateTime? expires = null)
    {
        var token = new JwtSecurityToken(issuer, audience, [new System.Security.Claims.Claim("sub", Guid.NewGuid().ToString())],
            DateTime.UtcNow.AddMinutes(-5), expires ?? DateTime.UtcNow.AddMinutes(5), new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = kid }, algorithm));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    private sealed class Sender(string document) : HttpMessageHandler
    {
        public string Document { get; set; } = document;
        public bool Unavailable { get; set; }
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(Unavailable ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
                { Content = new StringContent(Document) });
        }
    }
    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "CatalogTests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
