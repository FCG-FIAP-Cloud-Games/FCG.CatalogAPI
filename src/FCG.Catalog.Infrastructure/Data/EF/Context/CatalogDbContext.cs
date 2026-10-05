using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Infrastructure.Data.EF.Mappings;
using Microsoft.EntityFrameworkCore;

namespace FCG.Catalog.Infrastructure.Data.EF.Context;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<FCG.Catalog.Infrastructure.Outbox.MensagemOutbox> Outbox => Set<FCG.Catalog.Infrastructure.Outbox.MensagemOutbox>();
    public DbSet<FCG.Catalog.Domain.Orders.Pedido> Pedidos => Set<FCG.Catalog.Domain.Orders.Pedido>();
    public DbSet<FCG.Catalog.Domain.Library.Aquisicao> Aquisicoes => Set<FCG.Catalog.Domain.Library.Aquisicao>();
    public DbSet<Jogo> Jogos => Set<Jogo>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<CategoriaJogo> CategoriasJogos => Set<CategoriaJogo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AquisicaoMapping());
        modelBuilder.ApplyConfiguration(new PedidoMapping());
        modelBuilder.ApplyConfiguration(new MensagemOutboxMapping());
        modelBuilder.ApplyConfiguration(new MapeamentoJogo());
        modelBuilder.ApplyConfiguration(new MapeamentoCategoria());
        modelBuilder.ApplyConfiguration(new MapeamentoCategoriaJogo());
    }
}
