using Xunit;
using FCG.Catalog.Domain.Catalog.Entities;

namespace FCG.Catalog.UnitTests.Catalog.Categorias;

public sealed class TestesCategoria
{
    // Verifica se o construtor cria uma categoria quando recebe um identificador
    // válido e um nome válido, preservando os valores informados.
    [Fact]
    public void Construtor_ComDadosValidos_CriaCategoria()
    {
        // Arrange
        var id = Guid.NewGuid();
        const string nome = "Ação";

        // Act
        var categoria = new Categoria(id, nome);

        // Assert
        Assert.Equal(id, categoria.Id);
        Assert.Equal(nome, categoria.Nome);
    }

    // Verifica se o método AlterarNome atualiza o nome da categoria ao receber um novo nome válido.
    [Fact]
    public void AlterarNome_ComDadosValidos_AlteraNome()
    {
        // Arrange
        var categoria = new Categoria(Guid.NewGuid(), "Ação");
    
        // Act
        categoria.AlterarNome("Aventura");

        // Assert
        Assert.Equal("Aventura", categoria.Nome);
    }

    // Verifica se o método AlterarNome rejeita um nome vazio,
    // lançando ArgumentException e impedindo uma alteração inválida.
    [Fact]
    public void AlterarNome_ComNomeInvalido_LancaExcecao()
    {
        // Arrange
        var categoria = new Categoria(Guid.NewGuid(), "Ação");

        // Act e Assert
        Assert.Throws<ArgumentException>(() => categoria.AlterarNome(string.Empty));
    }

    // Verifica se o construtor rejeita Guid.Empty,
    // impedindo a criação de uma categoria sem identificador válido.
    [Fact]
    public void Construtor_ComGuidVazio_LancaExcecao()
    {
        // Arrange
        var id = Guid.Empty;
        const string nome = "Ação";

        // Act e Assert
        Assert.Throws<ArgumentException>(
            () => new Categoria(id, nome));
    }

    // Verifica se o método rejeita um nome composto apenas por espaços.
    [Fact]
    public void AlterarNome_ComNomeApenasEspacos_LancaExcecao()
    {
        // Arrange
        var categoria = new Categoria(Guid.NewGuid(), "Ação");

        // Act e Assert
        Assert.Throws<ArgumentException>(
            () => categoria.AlterarNome("   "));
    }  

    // Verifica se o método rejeita um nome com mais de 200 caracteres, lançando o ArgumentException.
    [Fact]
    public void AlterarNome_ComNomeMaiorQue200Caracteres_LancaExcecao()
    {
        // Arrange
        var categoria = new Categoria(Guid.NewGuid(), "Ação");

        // Act e Assert
        Assert.Throws<ArgumentException>(
            () => categoria.AlterarNome(new string('a', 201)));
    }

    // Verifica se o método AlterarNome normaliza o nome da categoria 
    // ao remover espaços em branco no início e no final.
    [Fact]
    public void AlterarNome_ComEspacosExternos_NormalizaNome()
    {
        // Arrange
        var categoria = new Categoria(Guid.NewGuid(), "Ação");

        // Act
        categoria.AlterarNome("   Aventura   ");

        // Assert
        Assert.Equal("Aventura", categoria.Nome);
    }

    // Verifica se o método AlterarNome mantém o estado anterior da categoria, 
    // em caso de alteração inválida.
    [Fact]
    public void AlterarNome_AlteracaoInvalida_MantemEstadoAnterior()
    {
        // Arrange
        var categoria = new Categoria(Guid.NewGuid(), "Ação");
        var nomeAnterior = categoria.Nome;

        // Act e Assert
        Assert.Throws<ArgumentException>(
            () => categoria.AlterarNome(string.Empty));
        Assert.Equal(nomeAnterior, categoria.Nome);
    }
}