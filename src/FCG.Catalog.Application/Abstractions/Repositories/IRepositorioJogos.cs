using FCG.Catalog.Domain.Catalog.Entities;

namespace FCG.Catalog.Application.Abstractions.Repositories;

/// <summary>
/// Define as operações de persistência necessárias pelos casos de uso do catálogo de jogos.
/// </summary>
public interface IRepositorioJogos
{
    Task<Jogo?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Jogo>> ListarAsync(
        int pagina,
        int tamanhoPagina,
        CancellationToken cancellationToken = default);

    Task AdicionarAsync(
        Jogo jogo,
        CancellationToken cancellationToken = default);

    Task AtualizarAsync(
        Jogo jogo,
        CancellationToken cancellationToken = default);
}
