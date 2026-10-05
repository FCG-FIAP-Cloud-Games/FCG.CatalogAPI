using FCG.Catalog.Application.Library;
using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Domain.Library;
using FCG.Catalog.Domain.Orders;
using FCG.Catalog.Tests;
using Xunit;

namespace FCG.Catalog.UnitTests;

public sealed class BibliotecaTests
{
    [Fact]
    public void Aquisicao_nova_e_historica_preservam_invariantes()
    {
        var user = Guid.NewGuid(); var game = Guid.NewGuid(); var order = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;
        var a = Aquisicao.Conceder(user, game, order);
        Assert.NotEqual(Guid.Empty, a.Id);
        Assert.Equal(user, a.UsuarioId); Assert.Equal(game, a.JogoId); Assert.Equal(order, a.PedidoId);
        Assert.InRange(a.DataAquisicao, before, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, a.DataAquisicao.Offset);
        var id = Guid.NewGuid(); var date = new DateTimeOffset(2020, 1, 2, 10, 0, 0, TimeSpan.FromHours(-3));
        var historical = Aquisicao.Historica(id, user, game, date);
        Assert.Equal(id, historical.Id); Assert.Null(historical.PedidoId);
        Assert.Equal(date, historical.DataAquisicao); Assert.Equal(TimeSpan.Zero, historical.DataAquisicao.Offset);
        Assert.Throws<ArgumentException>(() => Aquisicao.Conceder(user, game, Guid.Empty));
        Assert.Throws<ArgumentException>(() => Aquisicao.Conceder(Guid.Empty, game, order));
        Assert.Throws<ArgumentException>(() => Aquisicao.Conceder(user, Guid.Empty, order));
        Assert.Throws<ArgumentException>(() => Aquisicao.Historica(Guid.Empty, user, game, date));
    }

    [Theory]
    [InlineData("valid", ResultadoConcessao.Concedido)]
    [InlineData("inactive", ResultadoConcessao.Concedido)]
    [InlineData("duplicate", ResultadoConcessao.JaConcedido)]
    [InlineData("race", ResultadoConcessao.JaConcedido)]
    [InlineData("missing-game", ResultadoConcessao.JogoNaoEncontrado)]
    [InlineData("empty-user", ResultadoConcessao.IdentificadoresInvalidos)]
    [InlineData("empty-game", ResultadoConcessao.IdentificadoresInvalidos)]
    [InlineData("empty-order", ResultadoConcessao.IdentificadoresInvalidos)]
    [InlineData("missing-order", ResultadoConcessao.PedidoNaoEncontrado)]
    [InlineData("other-user", ResultadoConcessao.PedidoIncompativel)]
    [InlineData("other-game", ResultadoConcessao.PedidoIncompativel)]
    public async Task Concessao_valida_regras_sem_coordenar_transacao(string scenario, ResultadoConcessao expected)
    {
        var fake = new PedidoFakes();
        var game = new Jogo(Guid.NewGuid(), "Game", null, null, 10);
        var user = Guid.NewGuid();
        var order = new Pedido(scenario == "other-user" ? Guid.NewGuid() : user,
            scenario == "other-game" ? Guid.NewGuid() : game.Id, 10, Guid.NewGuid());
        if (scenario != "missing-game") fake.Jogos.Add(game);
        if (scenario == "inactive") typeof(Jogo).GetProperty(nameof(Jogo.Ativo))!.SetValue(game, false);
        if (scenario != "missing-order") fake.Pedidos.Add(order);
        var repo = new AquisicoesFake { Exists = scenario == "duplicate", Inserted = scenario != "race" };
        using var cancellation = new CancellationTokenSource();
        var result = await new ManipuladorConcederJogoAoUsuario(fake, fake, repo).ExecutarAsync(
            scenario == "empty-user" ? Guid.Empty : user, scenario == "empty-game" ? Guid.Empty : game.Id,
            scenario == "empty-order" ? Guid.Empty : order.Id, cancellation.Token);
        Assert.Equal(expected, result);
        Assert.Equal(StatusPedido.PendingPayment, order.Status);
        Assert.DoesNotContain("begin", fake.Chamadas); Assert.DoesNotContain("commit", fake.Chamadas);
        Assert.DoesNotContain("lock", fake.Chamadas); Assert.Empty(fake.Eventos);
        if (scenario is "valid" or "inactive" or "race")
        {
            Assert.Equal(user, repo.Added!.UsuarioId); Assert.Equal(game.Id, repo.Added.JogoId);
            Assert.Equal(order.Id, repo.Added.PedidoId); Assert.Equal(cancellation.Token, repo.Token);
        }
        else Assert.Null(repo.Added);
    }

    private sealed class AquisicoesFake : IRepositorioAquisicoes
    {
        public bool Exists { get; init; }
        public bool Inserted { get; init; }
        public Aquisicao? Added { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<bool> ExisteAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default) => Task.FromResult(Exists);
        public Task<bool> InserirAsync(Aquisicao aquisicao, CancellationToken cancellationToken = default)
        { Added = aquisicao; Token = cancellationToken; return Task.FromResult(Inserted); }
    }
}
