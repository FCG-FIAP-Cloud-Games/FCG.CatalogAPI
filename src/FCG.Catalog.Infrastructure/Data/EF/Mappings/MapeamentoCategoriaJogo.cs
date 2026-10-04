using FCG.Catalog.Domain.Catalog.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Catalog.Infrastructure.Data.EF.Mappings;

public class MapeamentoCategoriaJogo : IEntityTypeConfiguration<CategoriaJogo>
{
    public void Configure(EntityTypeBuilder<CategoriaJogo> builder)
    {
        builder.ToTable("rel_CategoriaJogo");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.JogoId)
            .IsRequired();

        builder.Property(x => x.CategoriaId)
            .IsRequired();

        builder.HasOne<Jogo>()
            .WithMany()
            .HasForeignKey(x => x.JogoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Categoria>()
            .WithMany()
            .HasForeignKey(x => x.CategoriaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.JogoId, x.CategoriaId })
            .IsUnique();
    }
}
