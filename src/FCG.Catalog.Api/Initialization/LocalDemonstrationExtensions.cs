using System.Data;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore;

namespace FCG.Catalog.Api.Initialization;

public static class LocalDemonstrationExtensions
{
    // Lock de sessão: permanece ativo entre as transações das migrations do EF Core 8.
    private const long MigrationLockId = 71870132;

    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment)
    {
        var value = configuration["DemonstracaoLocal:Habilitada"];
        if (value is null) return false;
        if (!bool.TryParse(value, out var enabled))
            throw new InvalidOperationException("DemonstracaoLocal:Habilitada deve ser true ou false.");
        if (enabled && !environment.IsDevelopment())
            throw new InvalidOperationException("DemonstracaoLocal:Habilitada=true exige o ambiente Development.");
        return enabled;
    }

    public static async Task PrepareLocalDemonstrationAsync(
        this WebApplication app, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(app.Configuration, app.Environment)) return;

        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database;
        // A infraestrutura cria o banco e seu usuário; a API cria apenas seu schema.
        await database.OpenConnectionAsync(cancellationToken);
        var locked = false;
        try
        {
            await database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock({MigrationLockId})", cancellationToken);
            locked = true;
            await database.MigrateAsync(cancellationToken);
            app.Logger.LogInformation("Migrations do Catalog concluídas para a demonstração local.");
        }
        finally
        {
            try
            {
                if (locked && database.GetDbConnection().State == ConnectionState.Open)
                    await database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock({MigrationLockId})", CancellationToken.None);
            }
            finally
            {
                await database.CloseConnectionAsync();
            }
        }
    }
}
