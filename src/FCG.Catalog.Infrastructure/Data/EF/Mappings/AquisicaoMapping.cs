using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Domain.Library;
using FCG.Catalog.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Catalog.Infrastructure.Data.EF.Mappings;

internal sealed class AquisicaoMapping : IEntityTypeConfiguration<Aquisicao>
{
    public void Configure(EntityTypeBuilder<Aquisicao> b)
    {
        b.ToTable("aquisicoes");
        b.HasKey(a => a.Id).HasName("pk_aquisicoes");
        b.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(a => a.UsuarioId).HasColumnName("usuario_id").IsRequired();
        b.Property(a => a.JogoId).HasColumnName("jogo_id").IsRequired();
        b.Property(a => a.DataAquisicao).HasColumnName("data_aquisicao")
            .HasColumnType("timestamp with time zone").IsRequired();
        b.Property(a => a.PedidoId).HasColumnName("pedido_id").IsRequired(false);
        b.HasOne<Jogo>().WithMany().HasForeignKey(a => a.JogoId)
            .HasConstraintName("fk_aquisicoes_jogos").OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Pedido>().WithMany().HasForeignKey(a => a.PedidoId)
            .HasConstraintName("fk_aquisicoes_pedidos").OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.UsuarioId, a.JogoId }).IsUnique().HasDatabaseName("ux_aquisicoes_usuario_jogo");
        b.HasIndex(a => a.PedidoId).IsUnique().HasFilter("pedido_id IS NOT NULL")
            .HasDatabaseName("ux_aquisicoes_pedido");
        b.HasIndex(a => a.JogoId).HasDatabaseName("ix_aquisicoes_jogo");
    }
}
