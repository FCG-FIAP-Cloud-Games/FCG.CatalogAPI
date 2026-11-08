using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using FCG.Catalog.Application.Orders;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore;

namespace FCG.Catalog.Infrastructure.Data.EF;

public sealed class LockUsuarioJogo(CatalogDbContext db) : ILockUsuarioJogo
{
    // Contrato compartilhável com C19: UTF8, UUIDs N minúsculos, namespace v1,
    // SHA256, primeiros 8 bytes como inteiro assinado big endian.
    public static long CalcularChave(Guid userId, Guid gameId) => BinaryPrimitives.ReadInt64BigEndian(
        SHA256.HashData(Encoding.UTF8.GetBytes($"catalog:usuario-jogo:v1:{userId:N}:{gameId:N}")));

    public async Task AcquireAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("O lock exige uma transação ativa no mesmo CatalogDbContext.");
        var key = CalcularChave(userId, gameId);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }
}
