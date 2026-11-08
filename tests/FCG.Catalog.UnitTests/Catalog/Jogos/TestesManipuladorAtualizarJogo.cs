using Xunit;
using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Application.Catalog.Jogos;
using FCG.Catalog.Domain.Catalog.Entities;

namespace FCG.Catalog.UnitTests.Catalog.Jogos;

public sealed class TestesManipuladorAtualizarJogo
{
    [Fact]
    public async Task Processar_ComDadosValidos_AtualizaJogoExistente()
    {
        var jogo = new Jogo(Guid.NewGuid(), "Título Antigo", "Descrição antiga", "10", 50m);
        var repositorio = new RepositorioJogosStub { Jogo = jogo };
        var manipulador = new ManipuladorAtualizarJogo(repositorio);

        var resultado = await manipulador.ProcessarAsync(
            new ComandoAtualizarJogo(jogo.Id, "Título Novo", "Descrição nova", "16", 75m),
            CancellationToken.None);

        Assert.Equal(StatusAtualizacaoJogo.Atualizado, resultado.Status);
        Assert.Equal("Título Novo", resultado.Jogo!.Titulo);
        Assert.Equal(75m, resultado.Jogo.Preco);
        Assert.True(repositorio.AtualizarFoiChamado);
    }

    [Fact]
    public async Task Processar_ComJogoInexistente_RetornaNaoEncontrado()
    {
        var repositorio = new RepositorioJogosStub();
        var manipulador = new ManipuladorAtualizarJogo(repositorio);

        var resultado = await manipulador.ProcessarAsync(
            new ComandoAtualizarJogo(Guid.NewGuid(), "Título", null, null, 10m),
            CancellationToken.None);

        Assert.Equal(StatusAtualizacaoJogo.NaoEncontrado, resultado.Status);
        Assert.False(repositorio.AtualizarFoiChamado);
    }

    [Fact]
    public async Task Processar_ComPrecoNegativo_NaoPersisteAlteracao()
    {
        var jogo = new Jogo(Guid.NewGuid(), "Título", null, null, 50m);
        var repositorio = new RepositorioJogosStub { Jogo = jogo };
        var manipulador = new ManipuladorAtualizarJogo(repositorio);

        var resultado = await manipulador.ProcessarAsync(
            new ComandoAtualizarJogo(jogo.Id, "Título", null, null, -10m),
            CancellationToken.None);

        Assert.Equal(StatusAtualizacaoJogo.DadosInvalidos, resultado.Status);
        Assert.True(resultado.Erros.ContainsKey("preco"));
        Assert.False(repositorio.AtualizarFoiChamado);
        Assert.Equal(50m, jogo.Preco);
    }

    [Fact]
    public async Task Processar_ComTituloVazio_NaoPersisteAlteracao()
    {
        var jogo = new Jogo(Guid.NewGuid(), "Título", null, null, 50m);
        var repositorio = new RepositorioJogosStub { Jogo = jogo };
        var manipulador = new ManipuladorAtualizarJogo(repositorio);

        var resultado = await manipulador.ProcessarAsync(
            new ComandoAtualizarJogo(jogo.Id, "   ", null, null, 50m),
            CancellationToken.None);

        Assert.Equal(StatusAtualizacaoJogo.DadosInvalidos, resultado.Status);
        Assert.True(resultado.Erros.ContainsKey("titulo"));
        Assert.False(repositorio.AtualizarFoiChamado);
        Assert.Equal("Título", jogo.Titulo);
    }

    private sealed class RepositorioJogosStub : IRepositorioJogos
    {
        public Jogo? Jogo { get; init; }

        public bool AtualizarFoiChamado { get; private set; }

        public Task<Jogo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Jogo is not null && Jogo.Id == id ? Jogo : null);

        public Task<IReadOnlyList<Jogo>> ListarAsync(
            int pagina,
            int tamanhoPagina,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Jogo>>([]);

        public Task AdicionarAsync(Jogo jogo, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AtualizarAsync(Jogo jogo, CancellationToken cancellationToken = default)
        {
            AtualizarFoiChamado = true;
            return Task.CompletedTask;
        }
    }
}
