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

        SecurityKey? publicKey = null;
        if (!string.IsNullOrWhiteSpace(jwt.PublicKeyPem))
        {
            if (jwt.PublicKeyPem.Contains("PRIVATE KEY", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Jwt:PublicKeyPem aceita somente chave pública RSA.");
            }

            using var rsa = RSA.Create();
            rsa.ImportFromPem(jwt.PublicKeyPem);
            publicKey = new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: false));
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
                    IssuerSigningKey = publicKey,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds)
                };
                // Sem chave, nenhuma assinatura é aceita. Não há Authority nem consulta externa.
            });

        return services;
    }
}
