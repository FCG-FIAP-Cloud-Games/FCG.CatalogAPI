using FCG.Catalog.Application.Library;
using FCG.Catalog.Application.Orders;
using FCG.Catalog.Infrastructure.Outbox;
using FCG.Catalog.Infrastructure.Repositories;
using FCG.Catalog.Infrastructure.Data.EF;
using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using FCG.Catalog.Infrastructure.Repositories.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.Catalog.Infrastructure.IoC;

public static class InfrastructureDependency
{
    public static IServiceCollection AddCatalogInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>(options =>
        {
            var connection = configuration.GetConnectionString("CatalogDatabase");
            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException(
                    "Configure ConnectionStrings:CatalogDatabase ou ConnectionStrings__CatalogDatabase.");
            options.UseNpgsql(connection);
        });
        services.AddScoped<IRepositorioJogos, RepositorioJogos>();
        services.AddScoped<IRepositorioPedidos, RepositorioPedidos>();
        services.AddScoped<IRepositorioAquisicoes, RepositorioAquisicoes>();
        services.AddScoped<ConsultaBiblioteca>();
        services.AddScoped<IConsultaBiblioteca>(p => p.GetRequiredService<ConsultaBiblioteca>());
        services.AddScoped<IConsultaListaBiblioteca>(p => p.GetRequiredService<ConsultaBiblioteca>());
        services.AddScoped<ILockUsuarioJogo, LockUsuarioJogo>();
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        services.Configure<OutboxOptions>(configuration.GetSection("Outbox"));
        services.AddScoped<RepositorioOutbox>();
        services.AddScoped<IRegistroOutbox>(p => p.GetRequiredService<RepositorioOutbox>());
        services.AddScoped<IRepositorioOutbox>(p => p.GetRequiredService<RepositorioOutbox>());
        services.AddSingleton<IPublicadorOutbox, RabbitMqPublisher>();
        services.AddScoped<EntregadorOutbox>();
        services.AddHostedService<OutboxWorker>();
        return services;
    }
}
