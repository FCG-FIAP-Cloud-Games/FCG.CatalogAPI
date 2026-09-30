using FCG.Catalog.Domain.Catalog.Entities;
using Xunit;

namespace FCG.Catalog.UnitTests.Catalog.Jogos;

public sealed class TestesJogo
{
    [Fact]
    public void Construtor_preserva_dados_e_cria_jogo_ativo_com_data_utc()
    {
        var before = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        var jogo = new Jogo(id, " Título ", null, null, 0);
        Assert.Equal(id, jogo.Id);
        Assert.Equal(" Título ", jogo.Titulo);
        Assert.Null(jogo.Descricao);
        Assert.Null(jogo.FaixaEtaria);
        Assert.Equal(0, jogo.Preco);
        Assert.True(jogo.Ativo);
        Assert.InRange(jogo.DataCadastro, before, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, jogo.DataCadastro.Offset);
    }

    [Fact]
    public void Construtor_rejeita_id_vazio() =>
        Assert.Throws<ArgumentException>(() => new Jogo(Guid.Empty, "Título", null, null, 1));

    [Theory]
    [InlineData(null, 1)]
    [InlineData("", 1)]
    [InlineData("   ", 1)]
    [InlineData("Título", -1)]
    public void Dados_invalidos_sao_rejeitados_sem_mutacao(string? titulo, decimal preco)
    {
        Assert.Throws<ArgumentException>(() => new Jogo(Guid.NewGuid(), titulo!, null, null, preco));
        var jogo = new Jogo(Guid.NewGuid(), "Original", "Descrição", "12", 10);
        Assert.Throws<ArgumentException>(() => jogo.AtualizarDados(titulo!, "Outro", "18", preco));
        Assert.Equal("Original", jogo.Titulo);
        Assert.Equal("Descrição", jogo.Descricao);
        Assert.Equal("12", jogo.FaixaEtaria);
        Assert.Equal(10, jogo.Preco);
    }

    [Fact]
    public void Atualizacao_preserva_identidade_data_e_estado()
    {
        var jogo = new Jogo(Guid.NewGuid(), "Original", null, null, 10);
        var id = jogo.Id;
        var data = jogo.DataCadastro;
        jogo.AtualizarDados("Novo", "Descrição", "18", 20);
        Assert.Equal(id, jogo.Id);
        Assert.Equal(data, jogo.DataCadastro);
        Assert.True(jogo.Ativo);
        Assert.Equal("Novo", jogo.Titulo);
        Assert.Equal("Descrição", jogo.Descricao);
        Assert.Equal("18", jogo.FaixaEtaria);
        Assert.Equal(20, jogo.Preco);
    }
}
