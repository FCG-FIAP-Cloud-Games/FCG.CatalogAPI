using FCG.Catalog.Application.Orders;
using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Domain.Orders;
using FCG.Catalog.Tests;
using Xunit;

namespace FCG.Catalog.UnitTests;

public sealed class PedidosTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static PedidoFakes Setup()
    {
        var fake = new PedidoFakes();
        fake.Jogos.Add(new Jogo(Guid.NewGuid(), "Jogo", null, null, 29.90m));
        return fake;
    }

    [Fact]
    public async Task Cria_preco_contratado_brl_pendente_e_ordem_transacional()
    {
        var fake = Setup();
        var before = DateTimeOffset.UtcNow;
        var result = await fake.Handler.ExecutarAsync(UserId, fake.Jogos[0].Id, Guid.NewGuid());
        Assert.Equal(202, result.Codigo);
        var pedido = Assert.Single(fake.Pedidos);
        Assert.NotEqual(Guid.Empty, pedido.Id);
        Assert.Equal(UserId, pedido.UserId);
        Assert.Equal("BRL", pedido.Currency);
        Assert.Equal(StatusPedido.PendingPayment, pedido.Status);
        Assert.InRange(pedido.CreatedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal(pedido.CreatedAt, pedido.UpdatedAt);
        fake.Jogos[0].AtualizarDados("Jogo", null, null, 99);
        Assert.Equal(29.90m, pedido.Price);
        Assert.Equal(new[] { "chave", "begin", "lock", "chave", "jogo", "posse", "pendente", "save", "commit", "dispose" }, fake.Chamadas);
    }

    [Theory]
    [InlineData(StatusPedido.PendingPayment, 202)]
    [InlineData(StatusPedido.Paid, 200)]
    [InlineData(StatusPedido.Rejected, 200)]
    public async Task Replay_precede_jogo_posse_e_pendente(StatusPedido status, int code)
    {
        var fake = Setup();
        var p = new Pedido(UserId, fake.Jogos[0].Id, 10, Guid.NewGuid());
        if (status != StatusPedido.PendingPayment) p.Finalizar(status);
        fake.Pedidos.Add(p);
        fake.Possui = true;
        fake.Jogos.Clear();
        var r = await fake.Handler.ExecutarAsync(UserId, p.GameId, p.IdempotencyKey);
        Assert.Equal(code, r.Codigo);
        Assert.Same(p, r.Pedido);
        Assert.Equal(new[] { "chave" }, fake.Chamadas);
    }

    [Theory]
    [InlineData("inexistente", 404)]
    [InlineData("inativo", 409)]
    [InlineData("posse", 409)]
    [InlineData("pendente", 409)]
    [InlineData("outra-chave-jogo", 409)]
    [InlineData("key-vazia", 400)]
    public async Task Valida_regras(string scenario, int code)
    {
        var fake = Setup();
        var game = fake.Jogos[0];
        var key = Guid.NewGuid();
        if (scenario == "inexistente") fake.Jogos.Clear();
        if (scenario == "inativo") typeof(Jogo).GetProperty(nameof(Jogo.Ativo))!.SetValue(game, false);
        fake.Possui = scenario == "posse";
        if (scenario == "pendente") fake.Pedidos.Add(new Pedido(UserId, game.Id, 10, Guid.NewGuid()));
        if (scenario == "outra-chave-jogo") fake.Pedidos.Add(new Pedido(UserId, Guid.NewGuid(), 10, key));
        if (scenario == "key-vazia") key = Guid.Empty;
        Assert.Equal(code, (await fake.Handler.ExecutarAsync(UserId, game.Id, key)).Codigo);
        Assert.DoesNotContain("save", fake.Chamadas);
    }

    [Fact]
    public async Task Reconsulta_chave_depois_do_lock()
    {
        var fake = Setup();
        var p = new Pedido(UserId, fake.Jogos[0].Id, 10, Guid.NewGuid());
        fake.AoAdquirirLock = () => fake.Pedidos.Add(p);
        var r = await fake.Handler.ExecutarAsync(UserId, p.GameId, p.IdempotencyKey);
        Assert.Same(p, r.Pedido);
        Assert.DoesNotContain("posse", fake.Chamadas);
    }

    [Fact]
    public async Task Unique_race_recupera_original_apos_rollback()
    {
        var fake = Setup();
        var original = new Pedido(UserId, fake.Jogos[0].Id, 10, Guid.NewGuid());
        fake.AoAdicionar = _ => { fake.Pedidos.Add(original); throw new ConflitoUnicoPedidoException(new Exception()); };
        var r = await fake.Handler.ExecutarAsync(UserId, original.GameId, original.IdempotencyKey);
        Assert.Same(original, r.Pedido);
        Assert.Equal(new[] { "dispose", "chave" }, fake.Chamadas.TakeLast(2));
    }

    [Theory]
    [InlineData("jogo", 404)]
    [InlineData("posse", 409)]
    [InlineData("pendente", 409)]
    public async Task Revalida_mudancas_ocorridas_antes_do_lock(string change, int code)
    {
        var fake = Setup();
        var game = fake.Jogos[0].Id;
        fake.AoAdquirirLock = () =>
        {
            if (change == "jogo") fake.Jogos.Clear();
            if (change == "posse") fake.Possui = true;
            if (change == "pendente") fake.Pedidos.Add(new Pedido(UserId, game, 1, Guid.NewGuid()));
        };
        Assert.Equal(code, (await fake.Handler.ExecutarAsync(UserId, game, Guid.NewGuid())).Codigo);
        Assert.DoesNotContain("save", fake.Chamadas);
    }

    [Theory]
    [InlineData(StatusPedido.Paid)]
    [InlineData(StatusPedido.Rejected)]
    public void Estados_terminais_nao_transicionam(StatusPedido status)
    {
        var p = new Pedido(UserId, Guid.NewGuid(), 1, Guid.NewGuid());
        p.Finalizar(status);
        Assert.True(p.UpdatedAt >= p.CreatedAt);
        Assert.Throws<InvalidOperationException>(() => p.Finalizar(StatusPedido.Paid));
        Assert.Throws<InvalidOperationException>(() => p.Finalizar(StatusPedido.Rejected));
        Assert.Throws<InvalidOperationException>(() => p.Finalizar(StatusPedido.PendingPayment));
    }

    [Fact]
    public void Dominio_rejeita_chave_vazia_e_preco_invalido()
    {
        Assert.Throws<ArgumentException>(() => new Pedido(UserId, Guid.NewGuid(), 1, Guid.Empty));
        Assert.Throws<ArgumentException>(() => new Pedido(UserId, Guid.NewGuid(), -1, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new Pedido(UserId, Guid.NewGuid(), 1.001m, Guid.NewGuid()));
    }
}
