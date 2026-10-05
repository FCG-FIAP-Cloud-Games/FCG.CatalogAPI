using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace FCG.Catalog.IntegrationTests;

public sealed class CatalogModelTests
{
    [Fact]
    public void Npgsql_model_and_migrations_contain_catalog_and_orders()
    {
        using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=catalog").Options);
        var model = db.GetService<IDesignTimeModel>().Model;
        Assert.Equal(new[] { "catalog_outbox", "jogos", "pedidos", "rel_CategoriaJogo", "tb_Categorias" },
            model.GetEntityTypes().Select(e => e.GetTableName()).OrderBy(n => n, StringComparer.Ordinal));
        foreach (var entity in model.GetEntityTypes())
        {
            var id = entity.FindProperty("Id")!;
            Assert.Equal(typeof(Guid), id.ClrType);
            Assert.Equal(ValueGenerated.Never, id.ValueGenerated);
        }
        var jogo = model.FindEntityType(typeof(Jogo))!;
        Assert.Equal(150, jogo.FindProperty("Titulo")!.GetMaxLength());
        Assert.Equal(500, jogo.FindProperty("Descricao")!.GetMaxLength());
        Assert.Equal(2, jogo.FindProperty("FaixaEtaria")!.GetMaxLength());
        Assert.True(jogo.FindProperty("Descricao")!.IsNullable);
        Assert.True(jogo.FindProperty("FaixaEtaria")!.IsNullable);
        Assert.Equal(18, jogo.FindProperty("Preco")!.GetPrecision());
        Assert.Equal(2, jogo.FindProperty("Preco")!.GetScale());
        Assert.Equal(200, model.FindEntityType(typeof(Categoria))!.FindProperty("Nome")!.GetMaxLength());
        var relation = model.FindEntityType(typeof(CategoriaJogo))!;
        Assert.Equal(2, relation.GetForeignKeys().Count());
        Assert.All(relation.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior));
        Assert.Contains(relation.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(new[] { "JogoId", "CategoriaId" }));
        Assert.Equal(3, db.Database.GetMigrations().Count());
        var sql = db.GetService<IMigrator>().GenerateScript();
        Assert.Equal(6, sql.Split("CREATE TABLE ").Length - 1); // Three catalog tables + orders + outbox + EF history.
        Assert.Contains("CREATE TABLE jogos", sql);
        Assert.Contains("CREATE TABLE \"tb_Categorias\"", sql);
        Assert.Contains("CREATE TABLE \"rel_CategoriaJogo\"", sql);
        Assert.Contains("numeric(18,2)", sql);
    }
}
