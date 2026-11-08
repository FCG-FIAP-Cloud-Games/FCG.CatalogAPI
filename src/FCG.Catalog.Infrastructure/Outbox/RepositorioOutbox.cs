using FCG.Catalog.Application.Orders;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore;
namespace FCG.Catalog.Infrastructure.Outbox;

public sealed class RepositorioOutbox(CatalogDbContext db) : IRepositorioOutbox
{
    public async Task AdicionarAsync(OrderPlacedEvent evento, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Outbox exige a transação de criação do Pedido.");
        var mensagem = MensagemOutbox.De(evento);
        db.Outbox.Add(mensagem);
        try { await db.SaveChangesAsync(ct); }
        catch { db.Entry(mensagem).State = EntityState.Detached; throw; }
    }

    public async Task<IReadOnlyList<MensagemOutbox>> ReservarAsync(int tamanho, TimeSpan duracao, CancellationToken ct)
    {
        if (tamanho < 1 || duracao <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(tamanho));
        var token = Guid.NewGuid();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.Outbox.FromSqlInterpolated($"""
            WITH candidatas AS (
                SELECT "Id" FROM catalog_outbox
                WHERE "PublishedAt" IS NULL
                  AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= now())
                  AND ("LockedUntil" IS NULL OR "LockedUntil" <= now())
                ORDER BY "OccurredAt", "Id"
                LIMIT {tamanho} FOR UPDATE SKIP LOCKED
            )
            UPDATE catalog_outbox AS o
            SET "LockToken" = {token}, "LockedUntil" = now() + {duracao},
                "Attempts" = CASE WHEN o."Attempts" < 2147483647 THEN o."Attempts" + 1 ELSE o."Attempts" END
            FROM candidatas c WHERE o."Id" = c."Id"
            RETURNING o.*
            """).AsNoTracking().ToListAsync(ct);
        await tx.CommitAsync(ct);
        return rows;
    }

    public async Task<bool> PublicadaAsync(Guid id, Guid token, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var count = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE catalog_outbox SET "PublishedAt" = now(), "LastError" = NULL,
                "NextAttemptAt" = NULL, "LockedUntil" = NULL, "LockToken" = NULL
            WHERE "Id" = {id} AND "LockToken" = {token} AND "PublishedAt" IS NULL AND "LockedUntil" > now()
            """, ct);
        await tx.CommitAsync(ct);
        return count == 1;
    }

    public async Task<bool> FalhouAsync(Guid id, Guid token, TimeSpan atraso, string erro, CancellationToken ct)
    {
        var resumo = erro.Length <= 512 ? erro : erro[..512];
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var count = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE catalog_outbox SET "LastError" = {resumo}, "NextAttemptAt" = now() + {atraso},
                "LockedUntil" = NULL, "LockToken" = NULL
            WHERE "Id" = {id} AND "LockToken" = {token} AND "PublishedAt" IS NULL AND "LockedUntil" > now()
            """, ct);
        await tx.CommitAsync(ct);
        return count == 1;
    }
}
