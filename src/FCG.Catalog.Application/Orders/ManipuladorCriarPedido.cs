using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Domain.Orders;

namespace FCG.Catalog.Application.Orders;

public sealed class ManipuladorCriarPedido(
    IRepositorioPedidos pedidos, IRepositorioJogos jogos,
    IConsultaBiblioteca biblioteca, ILockUsuarioJogo usuarioJogoLock)
{
    public async Task<ResultadoPedido> ExecutarAsync(Guid userId, Guid gameId, Guid key, CancellationToken ct = default)
    {
        if (userId == Guid.Empty || gameId == Guid.Empty || key == Guid.Empty)
            return new(null, 400, "Usuário, jogo e chave devem ser UUIDs não vazios.");
        var original = await pedidos.ObterPorChaveAsync(userId, key, ct);
        if (original is not null) return Recuperar(original, gameId);

        try
        {
            await using var transaction = await pedidos.IniciarTransacaoAsync(ct);
            await usuarioJogoLock.AcquireAsync(userId, gameId, ct);
            original = await pedidos.ObterPorChaveAsync(userId, key, ct);
            if (original is not null) return Recuperar(original, gameId);
            var jogo = await jogos.ObterPorIdAsync(gameId, ct);
            if (jogo is null) return new(null, 404, "Jogo não encontrado.");
            if (!jogo.Ativo) return new(null, 409, "Jogo indisponível.");
            if (await biblioteca.PossuiJogoAsync(userId, gameId, ct))
                return new(null, 409, "Usuário já possui o jogo.");
            if (await pedidos.ObterPendenteAsync(userId, gameId, ct) is not null)
                return new(null, 409, "Já existe pedido pendente para este jogo.");
            var pedido = new Pedido(userId, gameId, jogo.Preco, key);
            await pedidos.AdicionarAsync(pedido, ct);
            await transaction.CommitAsync(ct);
            return new(pedido, 202);
        }
        catch (ConflitoUnicoPedidoException)
        {
            // A transação foi desfeita antes da leitura. Também cobre a mesma chave
            // concorrendo por jogos distintos, que adquirem locks distintos.
            original = await pedidos.ObterPorChaveAsync(userId, key, ct);
            return original is not null ? Recuperar(original, gameId)
                : new(null, 409, "Já existe pedido pendente para este jogo.");
        }
    }

    private static ResultadoPedido Recuperar(Pedido pedido, Guid gameId) =>
        pedido.GameId != gameId ? new(null, 409, "Chave de idempotência já utilizada para outro jogo.")
        : new(pedido, pedido.Status == StatusPedido.PendingPayment ? 202 : 200);
}
