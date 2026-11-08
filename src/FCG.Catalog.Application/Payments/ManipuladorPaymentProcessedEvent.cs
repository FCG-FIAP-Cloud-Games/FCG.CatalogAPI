using FCG.Catalog.Application.Library;
using FCG.Catalog.Application.Orders;
using FCG.Catalog.Domain.Orders;

namespace FCG.Catalog.Application.Payments;

public sealed class ManipuladorPaymentProcessedEvent(
    IRepositorioProcessamentoPagamento repository, ILockUsuarioJogo usuarioJogoLock,
    ManipuladorConcederJogoAoUsuario concessao) : IProcessadorPagamento
{
    public const string ConsumerName = "Catalog.PaymentProcessedEvent";

    public async Task<ResultadoPagamento> ExecutarAsync(PaymentProcessedEvent e, CancellationToken ct = default)
    {
        ValidadorPaymentProcessedEvent.Validar(e);
        await using var tx = await repository.IniciarTransacaoAsync(ct);
        await usuarioJogoLock.AcquireAsync(e.UserId, e.GameId, ct);
        // Todas as leituras de negócio ocorrem após o lock, na mesma transação.
        if (await repository.ProcessadoAsync(ConsumerName, e.EventId, ct))
        {
            await tx.CommitAsync(ct);
            return ResultadoPagamento.Duplicado;
        }

        var pedido = await repository.ObterPedidoAsync(e.OrderId, ct)
            ?? throw new PagamentoInvalidoException("PedidoInexistente");
        if (pedido.UserId != e.UserId) throw new PagamentoInvalidoException("UserIdDivergente");
        if (pedido.GameId != e.GameId) throw new PagamentoInvalidoException("GameIdDivergente");
        if (pedido.Price != e.Amount) throw new PagamentoInvalidoException("AmountDivergente");
        if (pedido.Currency != e.Currency) throw new PagamentoInvalidoException("CurrencyDivergente");

        var destino = e.Status == "Approved" ? StatusPedido.Paid : StatusPedido.Rejected;
        if (pedido.Status != StatusPedido.PendingPayment && pedido.Status != destino)
            throw new PagamentoInvalidoException("ResultadoContraditorio");

        await ValidarPosseAsync(e, ct);
        var repetido = pedido.Status == destino;
        if (destino == StatusPedido.Paid)
        {
            // C18 não abre conexão/transação nem executa commit.
            var resultado = await concessao.ExecutarAsync(e.UserId, e.GameId, e.OrderId, ct);
            if (resultado is not (ResultadoConcessao.Concedido or ResultadoConcessao.JaConcedido))
                throw new PagamentoInvalidoException("Concessao" + resultado);
            // JaConcedido só é sucesso se as associações persistidas continuarem válidas.
            await ValidarPosseAsync(e, ct);
        }
        pedido.Finalizar(destino);
        repository.Registrar(ConsumerName, e.EventId);
        await tx.CommitAsync(ct);
        return repetido ? ResultadoPagamento.Compativel : ResultadoPagamento.Processado;
    }

    private async Task ValidarPosseAsync(PaymentProcessedEvent e, CancellationToken ct)
    {
        var porPedido = await repository.ObterPorPedidoAsync(e.OrderId, ct);
        if (porPedido is not null && (porPedido.UsuarioId != e.UserId || porPedido.JogoId != e.GameId))
            throw new PagamentoInvalidoException("PedidoReutilizadoNaAquisicao");
        if (porPedido is not null && e.Status == "Rejected")
            throw new PagamentoInvalidoException("PedidoRejeitadoComAquisicao");

        var posse = await repository.ObterPosseAsync(e.UserId, e.GameId, ct);
        if (posse?.PedidoId is Guid origemId && origemId != e.OrderId)
        {
            var origem = await repository.ObterPedidoAsync(origemId, ct);
            if (origem is null || origem.UserId != e.UserId || origem.GameId != e.GameId
                || origem.Status != StatusPedido.Paid)
                throw new PagamentoInvalidoException("PosseInconsistente");
        }
    }
}
