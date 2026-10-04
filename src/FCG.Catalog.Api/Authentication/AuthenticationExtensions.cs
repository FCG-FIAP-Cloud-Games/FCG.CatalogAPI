using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace FCG.Catalog.Api.Authentication;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddCatalogAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new();
        if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience)
            || jwt.ClockSkewSeconds < 0)
        {
            throw new InvalidOperationException("Configuração Jwt inválida: informe Issuer, Audience e ClockSkewSeconds não negativo.");
        }

        var keys = new Dictionary<string, SecurityKey>(StringComparer.Ordinal);
        foreach (var entry in jwt.PublicKeys)
        {
            if (string.IsNullOrWhiteSpace(entry.Kid) || string.IsNullOrWhiteSpace(entry.PublicKeyPem)
                || entry.PublicKeyPem.Contains("PRIVATE KEY", StringComparison.Ordinal)
                || keys.ContainsKey(entry.Kid))
            {
                throw new InvalidOperationException("Jwt:PublicKeys exige kid único e não vazio e somente chave pública RSA PEM.");
            }

            using var rsa = RSA.Create();
            rsa.ImportFromPem(entry.PublicKeyPem);
            keys.Add(entry.Kid, new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = entry.Kid });
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    TryAllIssuerSigningKeys = false,
                    IssuerSigningKeyResolver = (_, _, kid, _) =>
                        !string.IsNullOrWhiteSpace(kid) && keys.TryGetValue(kid, out var key)
                            ? [key] : Array.Empty<SecurityKey>(),
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds)
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var subjects = context.Principal?.FindAll("sub").ToArray() ?? [];
                        // O identificador externo é um Guid; não há consulta ou entidade de usuário.
                        if (subjects.Length != 1 || !Guid.TryParse(subjects[0].Value, out var subject)
                            || subject == Guid.Empty)
                        {
                            context.Fail("O token deve conter um único sub Guid válido e não vazio.");
                        }
                        return Task.CompletedTask;
                    }
                };
                // Apenas configuração local confiável: nenhum Authority, discovery ou fallback.
            });
        return services;
    }
}
