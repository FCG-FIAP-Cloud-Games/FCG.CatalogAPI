using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace FCG.Catalog.Api.Authentication;

public sealed class JwksKeyCache : BackgroundService
{
    public const string ClientName = "CatalogJwks";
    private sealed record Snapshot(Dictionary<string, SecurityKey> Keys, DateTimeOffset ExpiresAt);
    private readonly JwtOptions _options;
    private readonly IHttpClientFactory _clients;
    private readonly ILogger<JwksKeyCache> _logger;
    private readonly TimeProvider _clock;
    private readonly Uri _uri;
    private readonly SemaphoreSlim _refresh = new(0, 1);
    private Snapshot? _snapshot;
    private long _lastAttempt;

    public JwksKeyCache(JwtOptions options, IHttpClientFactory clients, ILogger<JwksKeyCache> logger,
        TimeProvider clock, bool allowHttp)
    {
        ValidateOptions(options);
        _uri = new Uri(options.JwksUri);
        if (_uri.Scheme != Uri.UriSchemeHttps && !allowHttp)
            throw new InvalidOperationException("JWKS exige HTTPS fora de Development.");
        _options = options; _clients = clients; _logger = logger; _clock = clock;
    }

    public static void ValidateOptions(JwtOptions options)
    {
        if (!Uri.TryCreate(options.JwksUri, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment)
            || !string.IsNullOrEmpty(uri.Query) || options.PublicKeys.Length != 0
            || options.JwksRefreshSeconds < 5 || options.JwksMaxAgeSeconds <= options.JwksRefreshSeconds)
            throw new InvalidOperationException("Configure uma URL JWKS confiável, sem chaves locais simultâneas, e prazos de cache válidos.");
    }

    public bool HasUsableKeys => Volatile.Read(ref _snapshot) is { } snapshot && snapshot.ExpiresAt > _clock.GetUtcNow();

    public SecurityKey[] Resolve(string? kid)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot is not null && snapshot.ExpiresAt > _clock.GetUtcNow()
            && kid is not null && snapshot.Keys.TryGetValue(kid, out var key))
            return [key];
        // O token nunca fornece uma URL. A atualização usa apenas o endereço configurado.
        if (_clock.GetUtcNow().UtcTicks - Interlocked.Read(ref _lastAttempt) >= TimeSpan.FromSeconds(5).Ticks)
        {
            try { _refresh.Release(); } catch (SemaphoreFullException) { }
        }
        return [];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var updated = await RefreshAsync(stoppingToken);
            await _refresh.WaitAsync(TimeSpan.FromSeconds(updated ? _options.JwksRefreshSeconds : 5), stoppingToken);
        }
    }

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _lastAttempt, _clock.GetUtcNow().UtcTicks);
        try
        {
            using var response = await _clients.CreateClient(ClientName).GetAsync(_uri,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            var buffer = new byte[65537];
            var length = 0;
            while (length < buffer.Length)
            {
                var count = await stream.ReadAsync(buffer.AsMemory(length), timeout.Token);
                if (count == 0) break;
                length += count;
            }
            if (length > 65536) throw new InvalidOperationException("JWKS excede o tamanho permitido.");
            using var json = JsonDocument.Parse(buffer.AsMemory(0, length));
            var entries = json.RootElement.GetProperty("keys");
            if (entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() is < 1 or > 10)
                throw new InvalidOperationException("Conjunto JWKS inválido.");
            var keys = new Dictionary<string, SecurityKey>(StringComparer.Ordinal);
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object
                    || entry.EnumerateObject().Any(p => p.Name is "d" or "p" or "q" or "dp" or "dq" or "qi" or "oth" or "k")
                    || entry.GetProperty("kty").GetString() != "RSA"
                    || entry.GetProperty("use").GetString() != "sig"
                    || entry.GetProperty("alg").GetString() != SecurityAlgorithms.RsaSha256)
                    throw new InvalidOperationException("JWKS deve conter somente chaves públicas RSA para RS256.");
                var kid = entry.GetProperty("kid").GetString();
                if (string.IsNullOrWhiteSpace(kid) || kid.Length > 128 || keys.ContainsKey(kid))
                    throw new InvalidOperationException("Identificador JWKS inválido ou duplicado.");
                var parameters = new RSAParameters
                {
                    Modulus = Base64UrlEncoder.DecodeBytes(entry.GetProperty("n").GetString()!),
                    Exponent = Base64UrlEncoder.DecodeBytes(entry.GetProperty("e").GetString()!)
                };
                using var rsa = RSA.Create();
                rsa.ImportParameters(parameters);
                if (rsa.KeySize < 2048) throw new InvalidOperationException("RSA exige pelo menos 2048 bits.");
                keys.Add(kid, new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = kid });
            }
            Volatile.Write(ref _snapshot, new Snapshot(keys, _clock.GetUtcNow().AddSeconds(_options.JwksMaxAgeSeconds)));
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Tempo excedido ao atualizar JWKS; mantendo somente cache ainda válido.");
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or CryptographicException
            or InvalidOperationException or FormatException or KeyNotFoundException or IOException or ArgumentException)
        {
            _logger.LogWarning("Não foi possível atualizar JWKS; mantendo somente cache ainda válido.");
            return false;
        }
    }
}
