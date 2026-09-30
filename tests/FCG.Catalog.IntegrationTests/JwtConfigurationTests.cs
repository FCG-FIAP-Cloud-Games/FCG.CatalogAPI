using System.Security.Cryptography;
using FCG.Catalog.Api.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FCG.Catalog.IntegrationTests;

public sealed class JwtConfigurationTests
{
    [Theory]
    [InlineData("missing-kid")]
    [InlineData("duplicate-kid")]
    [InlineData("empty-pem")]
    [InlineData("private-key")]
    [InlineData("invalid-pem")]
    [InlineData("negative-skew")]
    [InlineData("empty-issuer")]
    [InlineData("empty-audience")]
    public void Invalid_configuration_fails_at_registration(string scenario)
    {
        using var rsa = RSA.Create(2048);
        var values = new Dictionary<string, string?>
        {
            ["Jwt:PublicKeys:0:Kid"] = "users-key-1",
            ["Jwt:PublicKeys:0:PublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem()
        };
        switch (scenario)
        {
            case "missing-kid": values["Jwt:PublicKeys:0:Kid"] = " "; break;
            case "duplicate-kid":
                values["Jwt:PublicKeys:1:Kid"] = "users-key-1";
                values["Jwt:PublicKeys:1:PublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem();
                break;
            case "empty-pem": values["Jwt:PublicKeys:0:PublicKeyPem"] = ""; break;
            case "private-key": values["Jwt:PublicKeys:0:PublicKeyPem"] = rsa.ExportPkcs8PrivateKeyPem(); break;
            case "invalid-pem": values["Jwt:PublicKeys:0:PublicKeyPem"] = "invalid"; break;
            case "negative-skew": values["Jwt:ClockSkewSeconds"] = "-1"; break;
            case "empty-issuer": values["Jwt:Issuer"] = ""; break;
            case "empty-audience": values["Jwt:Audience"] = ""; break;
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        if (scenario == "invalid-pem")
            Assert.Throws<ArgumentException>(() => services.AddCatalogAuthentication(configuration));
        else
            Assert.Throws<InvalidOperationException>(() => services.AddCatalogAuthentication(configuration));
    }
}
