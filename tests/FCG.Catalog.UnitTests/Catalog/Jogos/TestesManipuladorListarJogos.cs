using Xunit;
using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Application.Catalog.Jogos;
using FCG.Catalog.Domain.Catalog.Entities;

namespace FCG.Catalog.UnitTests.Catalog.Jogos;

public sealed class TestesManipuladorListarJogos
{
    [Fact]
    public async Task Processar_ComJogosCadastrados_RetornaLista()
    {
        var jogos = new List<Jogo>
        {
            new(Guid.NewGuid(), "Jogo A", null, null, 10m),
            new(Guid.NewGuid(), "Jogo B", null, null, 20m)
        };
        var repositorio = new RepositorioJogosStub { Jogos = jogos };
        var manipulador = new ManipuladorListarJogos(repositorio);

        var resultado = await manipulador.ProcessarAsync(
            new ConsultaListarJogos(1, 20),
            CancellationToken.None);

        Assert.Equal(StatusListagemJogos.Sucesso, resultado.Status);
        Assert.Equal(2, resultado.Itens.Count);
    }

    [Fact]
    public async Task Processar_SemJogosCadastrados_RetornaListaVaziaSemErro()
    {
        var repositorio = new RepositorioJogosStub();
        var manipulador = new ManipuladorListarJogos(repositorio);

        var resultado = await manipulador.ProcessarAsync(
            new ConsultaListarJogos(1, 20),
            CancellationToken.None);

        Assert.Equal(StatusListagemJogos.Sucesso, resultado.Status);
        Assert.Empty(resultado.Itens);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 1000)]
    public async Task Processar_ComPaginacaoInvalida_RetornaErroSemConsultarRepositorio(
        int pagina,
        int tamanhoPagina)
    {
        var repositorio = new RepositorioJogosStub();
        var manipulador = new ManipuladorListarJogos(repositorio);

        var resultado = await manipulador.ProcessarAsync(
            new ConsultaListarJogos(pagina, tamanhoPagina),
            CancellationToken.None);

        Assert.Equal(StatusListagemJogos.PaginacaoInvalida, resultado.Status);
        Assert.NotEmpty(resultado.Erros);
        Assert.False(repositorio.ListarFoiChamado);
    }

    private sealed class RepositorioJogosStub : IRepositorioJogos
    {
        public IReadOnlyList<Jogo> Jogos { get; init; } = [];

        public bool ListarFoiChamado { get; private set; }

        public Task<Jogo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Jogo?>(null);

        public Task<IReadOnlyList<Jogo>> ListarAsync(
            int pagina,
            int tamanhoPagina,
            CancellationToken cancellationToken = default)
        {
            ListarFoiChamado = true;
            return Task.FromResult(Jogos);
        }

        public Task AdicionarAsync(Jogo jogo, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AtualizarAsync(Jogo jogo, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
