using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Domain.Catalog.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace FCG.Catalog.IntegrationTests;

public sealed class CatalogFactory : WebApplicationFactory<Program>
{
    private readonly RSA primary = RSA.Create(2048);
    private readonly RSA secondary = RSA.Create(2048);
    private readonly RSA untrusted = RSA.Create(2048);
    public string PublicKey => primary.ExportSubjectPublicKeyInfoPem();
    public RepositorioJogosFake Repository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:Issuer", "FIAP.CloudGames");
        builder.UseSetting("Jwt:Audience", "FIAP.CloudGames.Api");
        builder.UseSetting("Jwt:ClockSkewSeconds", "30");
        builder.UseSetting("Jwt:PublicKeys:0:Kid", "users-key-1");
        builder.UseSetting("Jwt:PublicKeys:0:PublicKeyPem", primary.ExportSubjectPublicKeyInfoPem());
        builder.UseSetting("Jwt:PublicKeys:1:Kid", "users-key-2");
        builder.UseSetting("Jwt:PublicKeys:1:PublicKeyPem", secondary.ExportSubjectPublicKeyInfoPem());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRepositorioJogos>();
            services.AddSingleton<IRepositorioJogos>(Repository);
        });
    }

    public string Token(string scenario = "valid", string? role = "Administrador", Guid? userId = null)
    {
        var now = DateTime.UtcNow;
        var rsa = scenario == "second-key" ? secondary : scenario == "bad-signature" ? untrusted : primary;
        var kid = scenario switch
        {
            "missing-kid" => null,
            "unknown-kid" => "unknown",
            "wrong-key" or "second-key" => "users-key-2",
            "kid-case" => "USERS-KEY-1",
            _ => "users-key-1"
        };
        var algorithm = scenario == "bad-algorithm" ? SecurityAlgorithms.RsaSha512 : SecurityAlgorithms.RsaSha256;
        SecurityKey key = new RsaSecurityKey(rsa) { KeyId = kid };
        if (scenario == "hmac")
        {
            key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)) { KeyId = kid };
            algorithm = SecurityAlgorithms.HmacSha256;
        }
        var subject = scenario switch
        {
            "empty-sub" => "",
            "whitespace-sub" => "   ",
            "invalid-sub" => "not-a-guid",
            "zero-sub" => Guid.Empty.ToString(),
            _ => (userId ?? Guid.NewGuid()).ToString()
        };
        var claims = new List<Claim>();
        if (scenario != "missing-sub") claims.Add(new Claim("sub", subject));
        if (scenario == "duplicate-sub") claims.Add(new Claim("sub", Guid.NewGuid().ToString()));
        if (role is not null) claims.Add(new Claim("role", role));
        var token = new JwtSecurityToken(
            issuer: scenario == "bad-issuer" ? "other" : "FIAP.CloudGames",
            audience: scenario == "bad-audience" ? "other" : "FIAP.CloudGames.Api",
            claims: claims,
            notBefore: scenario == "future-nbf" ? now.AddMinutes(2) :
                scenario == "nbf-within-skew" ? now.AddSeconds(15) : now.AddMinutes(-10),
            expires: scenario == "expired" ? now.AddMinutes(-2) :
                scenario == "exp-within-skew" ? now.AddSeconds(-5) : now.AddMinutes(5),
            signingCredentials: scenario == "unsigned" ? null : new SigningCredentials(key, algorithm));
        if (scenario == "missing-exp") token.Payload.Remove("exp");
        if (scenario == "missing-nbf") token.Payload.Remove("nbf");
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { primary.Dispose(); secondary.Dispose(); untrusted.Dispose(); }
    }
}

// Fake exclusivo dos testes de contrato do C14.
public sealed class RepositorioJogosFake : IRepositorioJogos
{
    public List<Jogo> Jogos { get; } = [];
    public int Writes { get; private set; }
    public Task<Jogo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Jogos.SingleOrDefault(j => j.Id == id));
    public Task<IReadOnlyList<Jogo>> ListarAsync(int pagina, int tamanhoPagina, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Jogo>>(Jogos.Skip((pagina - 1) * tamanhoPagina).Take(tamanhoPagina).ToArray());
    public Task AdicionarAsync(Jogo jogo, CancellationToken cancellationToken = default)
    {
        Jogos.Add(jogo);
        Writes++;
        return Task.CompletedTask;
    }
    public Task AtualizarAsync(Jogo jogo, CancellationToken cancellationToken = default)
    {
        Writes++;
        return Task.CompletedTask;
    }
}
