using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Application.Orders;
using FCG.Catalog.Domain.Library;

namespace FCG.Catalog.Application.Library;

// O caller coordena ILockUsuarioJogo e a transação compartilhada com Pedido/Inbox.
public sealed class ManipuladorConcederJogoAoUsuario(
    IRepositorioJogos jogos, IRepositorioPedidos pedidos, IRepositorioAquisicoes aquisicoes)
{
    public async Task<ResultadoConcessao> ExecutarAsync(
        Guid userId, Guid gameId, Guid pedidoId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty || gameId == Guid.Empty || pedidoId == Guid.Empty)
            return ResultadoConcessao.IdentificadoresInvalidos;
        if (await jogos.ObterPorIdAsync(gameId, ct) is null)
            return ResultadoConcessao.JogoNaoEncontrado;
        var pedido = await pedidos.ObterPorIdAsync(pedidoId, ct);
        if (pedido is null) return ResultadoConcessao.PedidoNaoEncontrado;
        if (pedido.UserId != userId || pedido.GameId != gameId)
            return ResultadoConcessao.PedidoIncompativel;
        if (await aquisicoes.ExisteAsync(userId, gameId, ct))
            return ResultadoConcessao.JaConcedido;
        return await aquisicoes.InserirAsync(Aquisicao.Conceder(userId, gameId, pedidoId), ct)
            ? ResultadoConcessao.Concedido : ResultadoConcessao.JaConcedido;
    }
}
