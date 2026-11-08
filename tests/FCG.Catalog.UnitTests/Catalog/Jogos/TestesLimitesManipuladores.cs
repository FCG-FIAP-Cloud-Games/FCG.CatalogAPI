using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Application.Catalog.Jogos;
using FCG.Catalog.Domain.Catalog.Entities;
using Xunit;

namespace FCG.Catalog.UnitTests.Catalog.Jogos;

public sealed class TestesLimitesManipuladores
{
    [Theory]
    [InlineData(150, true)]
    [InlineData(151, false)]
    public async Task Criacao_e_atualizacao_validam_limite_de_titulo(int length, bool valid)
    {
        var repository = new RepositorioObservavel();
        var title = new string('a', length);
        var created = await new ManipuladorCriarJogo(repository).ProcessarAsync(new(title, null, null, 0));
        var updated = await new ManipuladorAtualizarJogo(repository).ProcessarAsync(new(repository.Jogo.Id, title, null, null, 0));
        Assert.Equal(valid ? StatusCriacaoJogo.Criado : StatusCriacaoJogo.DadosInvalidos, created.Status);
        Assert.Equal(valid ? StatusAtualizacaoJogo.Atualizado : StatusAtualizacaoJogo.DadosInvalidos, updated.Status);
        Assert.Equal(valid ? 3 : 0, repository.Calls);
        if (!valid)
        {
            Assert.Contains("titulo", created.Erros.Keys);
            Assert.Contains("titulo", updated.Erros.Keys);
        }
    }

    [Fact]
    public async Task Casos_de_uso_propagam_cancelamento_e_parametros()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new RepositorioObservavel();
        var token = cancellation.Token;
        var created = await new ManipuladorCriarJogo(repository).ProcessarAsync(new("  Nome  ", " desc ", " 18 ", 0), token);
        Assert.Equal(token, repository.LastToken);
        Assert.Equal("Nome", created.Jogo!.Titulo);
        Assert.Equal("desc", created.Jogo.Descricao);
        Assert.Equal("18", created.Jogo.FaixaEtaria);
        Assert.NotEqual(Guid.Empty, created.Jogo.Id);
        var date = repository.Jogo.DataCadastro;
        var updated = await new ManipuladorAtualizarJogo(repository).ProcessarAsync(new(repository.Jogo.Id, " Novo ", " desc ", " 12 ", 2), token);
        Assert.Equal(token, repository.LastToken);
        Assert.Equal("Novo", updated.Jogo!.Titulo);
        Assert.Equal("desc", updated.Jogo.Descricao);
        Assert.Equal("12", updated.Jogo.FaixaEtaria);
        Assert.Equal(date, updated.Jogo.DataCadastro);
        await new ManipuladorObterJogoPorId(repository).ProcessarAsync(new(repository.Jogo.Id), token);
        Assert.Equal(repository.Jogo.Id, repository.LastId);
        Assert.Equal(token, repository.LastToken);
        var list = await new ManipuladorListarJogos(repository).ProcessarAsync(new(2, 100), token);
        Assert.Equal(token, repository.LastToken);
        Assert.Equal((2, 100), repository.Page);
        Assert.Equal(2, list.Pagina);
        Assert.Equal(100, list.TamanhoPagina);
        Assert.Single(list.Itens);
    }

    [Fact]
    public async Task Entradas_nulas_sao_rejeitadas_sem_acesso_ao_repositorio()
    {
        var repository = new RepositorioObservavel();
        await Assert.ThrowsAsync<ArgumentNullException>(() => new ManipuladorCriarJogo(repository).ProcessarAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => new ManipuladorAtualizarJogo(repository).ProcessarAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => new ManipuladorListarJogos(repository).ProcessarAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => new ManipuladorObterJogoPorId(repository).ProcessarAsync(null!));
        Assert.Equal(0, repository.Calls);
    }

    private sealed class RepositorioObservavel : IRepositorioJogos
    {
        public Jogo Jogo { get; } = new(Guid.NewGuid(), "Original", null, null, 1);
        public int Calls { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public Guid LastId { get; private set; }
        public (int, int) Page { get; private set; }
        private void Observe(CancellationToken token) { Calls++; LastToken = token; }
        public Task<Jogo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        { Observe(cancellationToken); LastId = id; return Task.FromResult<Jogo?>(Jogo); }
        public Task<IReadOnlyList<Jogo>> ListarAsync(int pagina, int tamanhoPagina, CancellationToken cancellationToken = default)
        { Observe(cancellationToken); Page = (pagina, tamanhoPagina); return Task.FromResult<IReadOnlyList<Jogo>>([Jogo]); }
        public Task AdicionarAsync(Jogo jogo, CancellationToken cancellationToken = default)
        { Observe(cancellationToken); return Task.CompletedTask; }
        public Task AtualizarAsync(Jogo jogo, CancellationToken cancellationToken = default)
        { Observe(cancellationToken); return Task.CompletedTask; }
    }
}
