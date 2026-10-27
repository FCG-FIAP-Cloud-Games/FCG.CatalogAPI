using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FCG.Catalog.Api.Authentication;

public sealed class JwksHealthCheck(JwksKeyCache cache) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(cache.HasUsableKeys ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Aguardando chaves públicas JWT válidas."));
}
