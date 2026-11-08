using System.Text.Json;
using FCG.Catalog.Application.Orders;
using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Domain.Orders;
using FCG.Catalog.Tests;
using Xunit;
namespace FCG.Catalog.UnitTests;

public sealed class OutboxTests
{
    [Fact]
    public async Task Evento_contem_contrato_snapshot_utc_e_replay_nao_cria_outro()
    {
        var f = new PedidoFakes();
        f.Jogos.Add(new Jogo(Guid.NewGuid(), "Jogo", null, null, 12.34m));
        var user = Guid.NewGuid(); var key = Guid.NewGuid();
        var result = await f.Handler.ExecutarAsync(user, f.Jogos[0].Id, key);
        var p = result.Pedido!; var e = Assert.Single(f.Eventos);
        Assert.NotEqual(Guid.Empty, e.EventId);
        Assert.Equal(f.Correlacao.CorrelationId, e.CorrelationId);
        Assert.Equal(1, e.Version); Assert.Equal(TimeSpan.Zero, e.OccurredAt.Offset);
        Assert.Equal(p.CreatedAt, e.OccurredAt);
        Assert.Equal((p.Id, p.UserId, p.GameId, p.Price, p.Currency), (e.OrderId, e.UserId, e.GameId, e.Price, e.Currency));
        var payload = JsonSerializer.Serialize(e);
        Assert.Equal(9, JsonDocument.Parse(payload).RootElement.EnumerateObject().Count());
        f.Jogos[0].AtualizarDados("Outro", null, null, 999);
        await f.Handler.ExecutarAsync(user, p.GameId, key);
        Assert.Same(e, Assert.Single(f.Eventos));
        Assert.Equal(payload, JsonSerializer.Serialize(e));
        Assert.NotEqual(e.EventId, OrderPlacedEvent.De(p, e.CorrelationId).EventId);
    }

    [Theory]
    [InlineData(1, 2)] [InlineData(2, 5)] [InlineData(3, 15)]
    [InlineData(4, 30)] [InlineData(5, 60)] [InlineData(100000, 60)]
    public void Backoff_tem_teto_sem_abandonar(int attempt, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), BackoffOutbox.Calcular(attempt));

    [Theory]
    [InlineData("absent")] [InlineData("invalid")] [InlineData("empty")] [InlineData("multiple")] [InlineData("valid")]
    public void Correlacao_respeita_header_ou_gera_uuid(string scenario)
    {
        var id = Guid.NewGuid(); var c = new ContextoCorrelacao();
        c.Definir(scenario switch { "absent" => [], "invalid" => ["abc"], "empty" => [Guid.Empty.ToString()],
            "multiple" => [id.ToString(), id.ToString()], _ => [id.ToString()] });
        Assert.NotEqual(Guid.Empty, c.CorrelationId);
        if (scenario == "valid") Assert.Equal(id, c.CorrelationId); else Assert.NotEqual(id, c.CorrelationId);
    }

    [Fact]
    public async Task Replay_apos_lock_e_conflito_nao_cria_evento()
    {
        foreach (var conflict in new[] { false, true })
        {
            var f = new PedidoFakes(); var game = new Jogo(Guid.NewGuid(), "G", null, null, 1); f.Jogos.Add(game);
            var p = new Pedido(Guid.NewGuid(), game.Id, 1, Guid.NewGuid());
            if (conflict) f.AoAdicionar = _ => { f.Pedidos.Add(p); throw new ConflitoUnicoPedidoException(new Exception()); };
            else f.AoAdquirirLock = () => f.Pedidos.Add(p);
            Assert.Same(p, (await f.Handler.ExecutarAsync(p.UserId, p.GameId, p.IdempotencyKey)).Pedido);
            Assert.Empty(f.Eventos);
        }
    }
}
