using FCG.Catalog.Application.Library;
using FCG.Catalog.Application.Orders;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore;

namespace FCG.Catalog.Infrastructure.Repositories;

public sealed class ConsultaBiblioteca(CatalogDbContext db) : IConsultaBiblioteca, IConsultaListaBiblioteca
{
    public Task<bool> PossuiJogoAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default) =>
        db.Aquisicoes.AsNoTracking().AnyAsync(a => a.UsuarioId == userId && a.JogoId == gameId, cancellationToken);

    public async Task<IReadOnlyList<ItemBiblioteca>> ListarAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await (from a in db.Aquisicoes.AsNoTracking()
               join j in db.Jogos.AsNoTracking() on a.JogoId equals j.Id
               where a.UsuarioId == userId
               orderby a.DataAquisicao descending, a.Id
               select new ItemBiblioteca(j.Id, j.Titulo, a.DataAquisicao)).ToListAsync(cancellationToken);
}
