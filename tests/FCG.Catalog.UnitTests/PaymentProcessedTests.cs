using FCG.Catalog.Application.Library;
using FCG.Catalog.Application.Orders;
using FCG.Catalog.Application.Payments;
using FCG.Catalog.Domain.Library;
using FCG.Catalog.Domain.Orders;
using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Tests;
using Xunit;

namespace FCG.Catalog.UnitTests;

public sealed class PaymentProcessedTests
{
    [Theory]
    [InlineData("Approved", StatusPedido.Paid, 1)]
    [InlineData("Rejected", StatusPedido.Rejected, 0)]
    public async Task Resultado_valido_e_idempotencia_por_evento_e_estado(string status, StatusPedido expected, int count)
    {
        var f = new Fixture(); var e = f.Event with { Status = status };
        // Preço atual não participa da comparação monetária.
        f.Orders.Jogos[0].AtualizarDados("Novo preço", null, null, 250);
        Assert.Equal(ResultadoPagamento.Processado, await f.Handler.ExecutarAsync(e));
        Assert.Equal(expected, f.Order.Status); Assert.Equal(count, f.Acquisitions.Count);
        if (count == 1) Assert.Equal(f.Order.Id, f.Acquisitions[0].PedidoId);
        var updated = f.Order.UpdatedAt;
        Assert.Equal(ResultadoPagamento.Duplicado, await f.Handler.ExecutarAsync(e));
        Assert.Equal(ResultadoPagamento.Compativel, await f.Handler.ExecutarAsync(e with { EventId = Guid.NewGuid() }));
        Assert.Equal(updated, f.Order.UpdatedAt); Assert.Equal(count, f.Acquisitions.Count);
        Assert.Equal(2, f.Inbox.Count);
        Assert.Equal(new[] { "begin", "lock", "inbox", "pedido" }, f.Calls.Take(4));
        Assert.Equal(3, f.Calls.Count(x => x == "commit"));
    }

    [Theory]
    [InlineData("Approved", "Rejected")]
    [InlineData("Rejected", "Approved")]
    public async Task Contradicao_nao_altera_estado_inbox_ou_posse(string first, string second)
    {
        var f = new Fixture(); await f.Handler.ExecutarAsync(f.Event with { Status = first });
        var state = f.Order.Status; var date = f.Order.UpdatedAt; var count = f.Acquisitions.Count;
        var ex = await Assert.ThrowsAsync<PagamentoInvalidoException>(() =>
            f.Handler.ExecutarAsync(f.Event with { EventId = Guid.NewGuid(), Status = second }));
        Assert.Equal("ResultadoContraditorio", ex.Motivo);
        Assert.Equal(state, f.Order.Status); Assert.Equal(date, f.Order.UpdatedAt);
        Assert.Single(f.Inbox); Assert.Equal(count, f.Acquisitions.Count);
    }

    [Theory]
    [InlineData("OrderId")]
    [InlineData("UserId")]
    [InlineData("GameId")]
    [InlineData("Amount")]
    [InlineData("Currency")]
    [InlineData("Status")]
    [InlineData("Version")]
    [InlineData("EventId")]
    [InlineData("CorrelationId")]
    [InlineData("PaymentId")]
    public async Task Invalidos_nao_produzem_efeitos(string field)
    {
        var f = new Fixture();
        var e = field switch
        {
            "OrderId" => f.Event with { OrderId = Guid.NewGuid() },
            "UserId" => f.Event with { UserId = Guid.NewGuid() },
            "GameId" => f.Event with { GameId = Guid.NewGuid() },
            "Amount" => f.Event with { Amount = 99.9001m },
            "Currency" => f.Event with { Currency = "USD" },
            "Status" => f.Event with { Status = "approved" },
            "Version" => f.Event with { Version = 2 },
            "EventId" => f.Event with { EventId = Guid.Empty },
            "CorrelationId" => f.Event with { CorrelationId = Guid.Empty },
            _ => f.Event with { PaymentId = Guid.Empty }
        };
        await Assert.ThrowsAsync<PagamentoInvalidoException>(() => f.Handler.ExecutarAsync(e));
        Assert.Equal(StatusPedido.PendingPayment, f.Order.Status);
        Assert.Empty(f.Inbox); Assert.Empty(f.Acquisitions); Assert.DoesNotContain("commit", f.Calls);
    }

    [Fact]
    public async Task Historico_compativel_preservado_mas_pedido_reutilizado_rejeitado()
    {
        var f = new Fixture();
        var history = Aquisicao.Historica(Guid.NewGuid(), f.Order.UserId, f.Order.GameId, DateTimeOffset.UtcNow);
        f.Acquisitions.Add(history);
        await f.Handler.ExecutarAsync(f.Event);
        Assert.Same(history, Assert.Single(f.Acquisitions));
        var bad = new Fixture();
        bad.Acquisitions.Add(Aquisicao.Conceder(Guid.NewGuid(), bad.Order.GameId, bad.Order.Id));
        await Assert.ThrowsAsync<PagamentoInvalidoException>(() => bad.Handler.ExecutarAsync(bad.Event));
        Assert.Empty(bad.Inbox); Assert.Equal(StatusPedido.PendingPayment, bad.Order.Status);
    }

    [Fact]
    public void Correlacao_recebida_independe_de_http()
    {
        var c = new ContextoCorrelacao(); var id = Guid.NewGuid();
        c.DefinirRecebido(id); Assert.Equal(id, c.CorrelationId);
        Assert.Throws<ArgumentException>(() => c.DefinirRecebido(Guid.Empty));
        Assert.Equal(id, c.CorrelationId);
    }

    private sealed class Fixture : IRepositorioProcessamentoPagamento, IRepositorioAquisicoes, ILockUsuarioJogo
    {
        public PedidoFakes Orders { get; } = new();
        public List<Aquisicao> Acquisitions { get; } = [];
        public HashSet<Guid> Inbox { get; } = [];
        public List<string> Calls { get; } = [];
        public Pedido Order { get; }
        public PaymentProcessedEvent Event { get; }
        public ManipuladorPaymentProcessedEvent Handler => new(this, this, new(Orders, Orders, this));
        public Fixture()
        {
            var game = new Jogo(Guid.NewGuid(), "Jogo", null, null, 99.90m);
            Order = new Pedido(Guid.NewGuid(), game.Id, game.Preco, Guid.NewGuid());
            Orders.Pedidos.Add(Order); Orders.Jogos.Add(game);
            Event = new PaymentProcessedEvent { EventId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(),
                OccurredAt = DateTimeOffset.UtcNow, Version = 1, PaymentId = Guid.NewGuid(),
                OrderId = Order.Id, UserId = Order.UserId, GameId = Order.GameId,
                Amount = Order.Price, Currency = "BRL", Status = "Approved" };
        }
        public Task<ITransacaoPedido> IniciarTransacaoAsync(CancellationToken ct)
        { Calls.Add("begin"); return Task.FromResult<ITransacaoPedido>(new Tx(Calls)); }
        public Task AcquireAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
        { Calls.Add("lock"); return Task.CompletedTask; }
        public Task<bool> ProcessadoAsync(string name, Guid id, CancellationToken ct)
        { Assert.Equal(ManipuladorPaymentProcessedEvent.ConsumerName, name); Calls.Add("inbox"); return Task.FromResult(Inbox.Contains(id)); }
        public Task<Pedido?> ObterPedidoAsync(Guid id, CancellationToken ct)
        { Calls.Add("pedido"); return Orders.ObterPorIdAsync(id, ct); }
        public Task<Aquisicao?> ObterPosseAsync(Guid u, Guid g, CancellationToken ct) =>
            Task.FromResult(Acquisitions.SingleOrDefault(a => a.UsuarioId == u && a.JogoId == g));
        public Task<Aquisicao?> ObterPorPedidoAsync(Guid p, CancellationToken ct) =>
            Task.FromResult(Acquisitions.SingleOrDefault(a => a.PedidoId == p));
        public void Registrar(string name, Guid id) { Inbox.Add(id); Calls.Add("registrar"); }
        public Task<bool> ExisteAsync(Guid u, Guid g, CancellationToken cancellationToken = default) =>
            Task.FromResult(Acquisitions.Any(a => a.UsuarioId == u && a.JogoId == g));
        public Task<bool> InserirAsync(Aquisicao a, CancellationToken cancellationToken = default)
        { Acquisitions.Add(a); return Task.FromResult(true); }
        private sealed class Tx(List<string> calls) : ITransacaoPedido
        {
            public Task CommitAsync(CancellationToken cancellationToken = default) { calls.Add("commit"); return Task.CompletedTask; }
            public ValueTask DisposeAsync() { calls.Add("dispose"); return ValueTask.CompletedTask; }
        }
    }
}
