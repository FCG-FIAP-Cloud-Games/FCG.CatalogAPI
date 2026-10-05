using FCG.Catalog.Domain.Library;

namespace FCG.Catalog.Application.Library;

public sealed record ItemBiblioteca(Guid GameId, string Title, DateTimeOffset AcquiredAt);

public interface IConsultaListaBiblioteca
{
    Task<IReadOnlyList<ItemBiblioteca>> ListarAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IRepositorioAquisicoes
{
    Task<bool> ExisteAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);
    // Exige transação externa ativa. Não adquire lock nem confirma a transação.
    // true = inserida; false = o par usuário/jogo já existe.
    Task<bool> InserirAsync(Aquisicao aquisicao, CancellationToken cancellationToken = default);
}

public enum ResultadoConcessao
{
    Concedido, JaConcedido, IdentificadoresInvalidos, JogoNaoEncontrado,
    PedidoNaoEncontrado, PedidoIncompativel
}
