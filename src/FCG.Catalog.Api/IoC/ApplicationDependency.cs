using FCG.Catalog.Application.Orders;
using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Application.Catalog.Jogos;

namespace FCG.Catalog.Api.IoC;

public static class ApplicationDependency
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        services.AddScoped<ContextoCorrelacao>();
        services.AddScoped<IContextoCorrelacao>(p => p.GetRequiredService<ContextoCorrelacao>());
        // Factories adiam a resolução do repositório até uma operação de catálogo.
        // Health e Swagger continuam independentes da conexão com o banco.
        services.AddScoped(provider => new ManipuladorCriarJogo(provider.GetRequiredService<IRepositorioJogos>()));
        services.AddScoped(provider => new ManipuladorAtualizarJogo(provider.GetRequiredService<IRepositorioJogos>()));
        services.AddScoped(provider => new ManipuladorObterJogoPorId(provider.GetRequiredService<IRepositorioJogos>()));
        services.AddScoped(provider => new ManipuladorListarJogos(provider.GetRequiredService<IRepositorioJogos>()));
        services.AddScoped(p => new ManipuladorCriarPedido(p.GetRequiredService<IRepositorioPedidos>(), p.GetRequiredService<IRepositorioJogos>(), p.GetRequiredService<IConsultaBiblioteca>(), p.GetRequiredService<ILockUsuarioJogo>(), p.GetRequiredService<IRegistroOutbox>(), p.GetRequiredService<IContextoCorrelacao>()));
        return services;
    }
}
