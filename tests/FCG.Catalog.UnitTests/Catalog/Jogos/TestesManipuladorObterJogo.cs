using Xunit;
using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Application.Catalog.Jogos;
using FCG.Catalog.Domain.Catalog.Entities;

namespace FCG.Catalog.UnitTests.Catalog.Jogos;

public sealed class TestesManipuladorObterJogo
{
    [Fact]
    public async Task Processar_ComJogoExistente_RetornaDadosDoJogo()
    {
        var jogo = new Jogo(Guid.NewGuid(), "Elden Ring", "Souls-like", "18", 249.90m);
        var repositorio = new RepositorioJogosStub { Jogo = jogo };
        var manipulador = new ManipuladorObterJogoPorId(repositorio);

        var resultado = await manipulador.ProcessarAsync(
            new ConsultaObterJogoPorId(jogo.Id),
            CancellationToken.None);

        Assert.Equal(StatusConsultaJogo.Encontrado, resultado.Status);
        Assert.NotNull(resultado.Jogo);
        Assert.Equal(jogo.Id, resultado.Jogo.Id);
        Assert.Equal(jogo.Titulo, resultado.Jogo.Titulo);
    }

    [Fact]
    public async Task Processar_ComJogoInexistente_RetornaNaoEncontrado()
    {
        var repositorio = new RepositorioJogosStub();
        var manipulador = new ManipuladorObterJogoPorId(repositorio);

        var resultado = await manipulador.ProcessarAsync(
            new ConsultaObterJogoPorId(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(StatusConsultaJogo.NaoEncontrado, resultado.Status);
        Assert.Null(resultado.Jogo);
    }

    [Fact]
    public async Task Processar_ComIdVazio_RetornaIdInvalido()
    {
        var repositorio = new RepositorioJogosStub();
        var manipulador = new ManipuladorObterJogoPorId(repositorio);

        var resultado = await manipulador.ProcessarAsync(
            new ConsultaObterJogoPorId(Guid.Empty),
            CancellationToken.None);

        Assert.Equal(StatusConsultaJogo.IdInvalido, resultado.Status);
        Assert.Null(resultado.Jogo);
    }

    private sealed class RepositorioJogosStub : IRepositorioJogos
    {
        public Jogo? Jogo { get; init; }

        public Task<Jogo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Jogo is not null && Jogo.Id == id ? Jogo : null);

        public Task<IReadOnlyList<Jogo>> ListarAsync(
            int pagina,
            int tamanhoPagina,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Jogo>>([]);

        public Task AdicionarAsync(Jogo jogo, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AtualizarAsync(Jogo jogo, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
