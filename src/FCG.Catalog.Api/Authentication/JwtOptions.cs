namespace FCG.Catalog.Api.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = "FIAP.CloudGames";
    public string Audience { get; init; } = "FIAP.CloudGames.Api";
    public int ClockSkewSeconds { get; init; } = 30;
    public JwtPublicKeyOptions[] PublicKeys { get; init; } = [];
}

public sealed class JwtPublicKeyOptions
{
    public string Kid { get; init; } = string.Empty;
    public string PublicKeyPem { get; init; } = string.Empty;
}
