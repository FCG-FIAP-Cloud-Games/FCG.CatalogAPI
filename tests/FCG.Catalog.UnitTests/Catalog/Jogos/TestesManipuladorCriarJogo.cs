using Xunit;
using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Application.Catalog.Jogos;
using FCG.Catalog.Domain.Catalog.Entities;

namespace FCG.Catalog.UnitTests.Catalog.Jogos;

public sealed class TestesManipuladorCriarJogo
{
    [Fact]
    public async Task Processar_ComDadosValidos_CriaJogoAtivo()
    {
        var repositorio = new RepositorioJogosStub();
        var manipulador = new ManipuladorCriarJogo(repositorio);

        var resultado = await manipulador.ProcessarAsync(
            new ComandoCriarJogo("  The Witcher 3  ", "RPG de mundo aberto", "18", 99.90m),
            CancellationToken.None);

        Assert.Equal(StatusCriacaoJogo.Criado, resultado.Status);
        Assert.NotNull(resultado.Jogo);
        Assert.Equal("The Witcher 3", resultado.Jogo.Titulo);
        Assert.True(resultado.Jogo.Ativo);
        Assert.NotNull(repositorio.JogoAdicionado);
    }

    [Fact]
    public async Task Processar_ComTituloVazio_RetornaDadosInvalidosSemPersistir()
    {
        var repositorio = new RepositorioJogosStub();
        var manipulador = new ManipuladorCriarJogo(repositorio);

        var resultado = await manipulador.ProcessarAsync(
            new ComandoCriarJogo("   ", null, null, 10m),
            CancellationToken.None);

        Assert.Equal(StatusCriacaoJogo.DadosInvalidos, resultado.Status);
        Assert.True(resultado.Erros.ContainsKey("titulo"));
        Assert.Null(repositorio.JogoAdicionado);
    }

    [Fact]
    public async Task Processar_ComPrecoNegativo_RetornaDadosInvalidosSemPersistir()
    {
        var repositorio = new RepositorioJogosStub();
        var manipulador = new ManipuladorCriarJogo(repositorio);

        var resultado = await manipulador.ProcessarAsync(
            new ComandoCriarJogo("Jogo Válido", null, null, -1m),
            CancellationToken.None);

        Assert.Equal(StatusCriacaoJogo.DadosInvalidos, resultado.Status);
        Assert.True(resultado.Erros.ContainsKey("preco"));
        Assert.Null(repositorio.JogoAdicionado);
    }

    private sealed class RepositorioJogosStub : IRepositorioJogos
    {
        public Jogo? JogoAdicionado { get; private set; }

        public Task<Jogo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Jogo?>(null);

        public Task<IReadOnlyList<Jogo>> ListarAsync(
            int pagina,
            int tamanhoPagina,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Jogo>>([]);

        public Task AdicionarAsync(Jogo jogo, CancellationToken cancellationToken = default)
        {
            JogoAdicionado = jogo;
            return Task.CompletedTask;
        }

        public Task AtualizarAsync(Jogo jogo, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
