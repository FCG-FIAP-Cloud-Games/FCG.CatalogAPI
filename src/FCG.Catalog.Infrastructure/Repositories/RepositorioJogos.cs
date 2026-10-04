using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Domain.Catalog.Entities;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore;

namespace FCG.Catalog.Infrastructure.Repositories.Catalog;

internal sealed class RepositorioJogos : IRepositorioJogos
{
    private readonly CatalogDbContext _contexto;

    public RepositorioJogos(CatalogDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task<Jogo?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        _contexto.Jogos.FirstOrDefaultAsync(jogo => jogo.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Jogo>> ListarAsync(
        int pagina,
        int tamanhoPagina,
        CancellationToken cancellationToken = default)
    {
        var jogos = await _contexto.Jogos
            .AsNoTracking()
            .OrderBy(jogo => jogo.Titulo)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken);

        return jogos;
    }

    public async Task AdicionarAsync(
        Jogo jogo,
        CancellationToken cancellationToken = default)
    {
        _contexto.Jogos.Add(jogo);
        await _contexto.SaveChangesAsync(cancellationToken);
    }

    public async Task AtualizarAsync(
        Jogo jogo,
        CancellationToken cancellationToken = default)
    {
        if (_contexto.Entry(jogo).State == EntityState.Detached)
        {
            _contexto.Jogos.Update(jogo);
        }

        await _contexto.SaveChangesAsync(cancellationToken);
    }
}
