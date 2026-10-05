using FCG.Catalog.Application.Orders;
using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Domain.Orders;
using FCG.Catalog.Domain.Catalog.Entities;

namespace FCG.Catalog.Tests;

public sealed class PedidoFakes : IRepositorioPedidos, IRepositorioJogos, IConsultaBiblioteca, ILockUsuarioJogo, IRegistroOutbox
{
    public List<OrderPlacedEvent> Eventos { get; } = [];
    public ContextoCorrelacao Correlacao { get; } = new();
    public Task AdicionarAsync(OrderPlacedEvent evento, CancellationToken ct = default)
    { Chamadas.Add("outbox"); Eventos.Add(evento); return Task.CompletedTask; }
    public List<Pedido> Pedidos { get; } = [];
    public List<Jogo> Jogos { get; } = [];
    public List<string> Chamadas { get; } = [];
    public bool Possui { get; set; }
    public Action? AoAdquirirLock { get; set; }
    public Action<Pedido>? AoAdicionar { get; set; }
    public ManipuladorCriarPedido Handler => new(this, this, this, this, this, Correlacao);
    public Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Pedidos.SingleOrDefault(p => p.Id == id));
    public Task<Pedido?> ObterPorChaveAsync(Guid userId, Guid key, CancellationToken cancellationToken = default)
    {
        Chamadas.Add("chave");
        return Task.FromResult(Pedidos.SingleOrDefault(p => p.UserId == userId && p.IdempotencyKey == key));
    }
    public Task<Pedido?> ObterPendenteAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        Chamadas.Add("pendente");
        return Task.FromResult(Pedidos.SingleOrDefault(p => p.UserId == userId && p.GameId == gameId && p.Status == StatusPedido.PendingPayment));
    }
    public Task AdicionarAsync(Pedido pedido, CancellationToken cancellationToken = default)
    {
        Chamadas.Add("save");
        AoAdicionar?.Invoke(pedido);
        Pedidos.Add(pedido);
        return Task.CompletedTask;
    }
    public Task<ITransacaoPedido> IniciarTransacaoAsync(CancellationToken cancellationToken = default)
    {
        Chamadas.Add("begin");
        return Task.FromResult<ITransacaoPedido>(new Transaction(Chamadas));
    }
    public Task AcquireAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        Chamadas.Add("lock");
        AoAdquirirLock?.Invoke();
        return Task.CompletedTask;
    }
    public Task<bool> PossuiJogoAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        Chamadas.Add("posse");
        return Task.FromResult(Possui);
    }
    Task<Jogo?> IRepositorioJogos.ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Chamadas.Add("jogo");
        return Task.FromResult(Jogos.SingleOrDefault(j => j.Id == id));
    }
    public Task<IReadOnlyList<Jogo>> ListarAsync(int pagina, int tamanhoPagina, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Jogo>>(Jogos);
    public Task AdicionarAsync(Jogo jogo, CancellationToken cancellationToken = default) { Jogos.Add(jogo); return Task.CompletedTask; }
    public Task AtualizarAsync(Jogo jogo, CancellationToken cancellationToken = default) => Task.CompletedTask;
    private sealed class Transaction(List<string> chamadas) : ITransacaoPedido
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) { chamadas.Add("commit"); return Task.CompletedTask; }
        public ValueTask DisposeAsync() { chamadas.Add("dispose"); return ValueTask.CompletedTask; }
    }
}
