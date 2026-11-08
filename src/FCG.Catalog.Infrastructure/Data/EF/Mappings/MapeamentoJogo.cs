using FCG.Catalog.Domain.Catalog.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Catalog.Infrastructure.Data.EF.Mappings;

internal sealed class MapeamentoJogo : IEntityTypeConfiguration<Jogo>
{
    public void Configure(EntityTypeBuilder<Jogo> construtor)
    {
        construtor.ToTable("jogos");

        construtor.HasKey(jogo => jogo.Id)
            .HasName("pk_jogos");

        construtor.Property(jogo => jogo.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        construtor.Property(jogo => jogo.Titulo)
            .HasColumnName("titulo")
            .HasMaxLength(150)
            .IsRequired();

        construtor.Property(jogo => jogo.Descricao)
            .HasColumnName("descricao")
            .HasMaxLength(500)
            .IsRequired(false);

        construtor.Property(jogo => jogo.FaixaEtaria)
            .HasColumnName("faixa_etaria")
            .HasMaxLength(2)
            .IsRequired(false);

        construtor.Property(jogo => jogo.Preco)
            .HasColumnName("preco")
            .HasPrecision(18, 2)
            .IsRequired();

        construtor.Property(jogo => jogo.Ativo)
            .HasColumnName("ativo")
            .HasColumnType("boolean")
            .HasDefaultValue(true)
            .IsRequired();

        construtor.Property(jogo => jogo.DataCadastro)
            .HasColumnName("data_cadastro")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
