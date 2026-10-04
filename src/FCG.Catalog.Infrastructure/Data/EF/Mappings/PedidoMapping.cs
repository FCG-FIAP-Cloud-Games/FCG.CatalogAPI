using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Catalog.Infrastructure.Data.EF.Mappings;

internal sealed class PedidoMapping : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("pedidos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.UserId).IsRequired();
        builder.Property(p => p.GameId).IsRequired();
        builder.Property(p => p.Price).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.IdempotencyKey).IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(p => new { p.UserId, p.IdempotencyKey }).IsUnique().HasDatabaseName("ux_pedidos_usuario_chave");
        builder.HasIndex(p => new { p.UserId, p.GameId }).IsUnique()
            .HasFilter("\"Status\" = 'PendingPayment'").HasDatabaseName("ux_pedidos_usuario_jogo_pendente");
        builder.HasOne<Jogo>().WithMany().HasForeignKey(p => p.GameId).OnDelete(DeleteBehavior.Restrict);
    }
}
