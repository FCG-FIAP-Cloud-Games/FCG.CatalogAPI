using FCG.Catalog.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FCG.Catalog.Infrastructure.Data.EF.Mappings;

internal sealed class MensagemOutboxMapping : IEntityTypeConfiguration<MensagemOutbox>
{
    public void Configure(EntityTypeBuilder<MensagemOutbox> b)
    {
        b.ToTable("catalog_outbox", t => t.HasCheckConstraint("ck_outbox_attempts", "\"Attempts\" >= 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.EventType).HasMaxLength(200).IsRequired();
        b.Property(x => x.Payload).HasColumnType("text").IsRequired();
        b.Property(x => x.LastError).HasColumnType("text");
        b.Property(x => x.Attempts).HasDefaultValue(0);
        b.HasIndex(x => x.EventId).IsUnique();
        b.HasIndex(x => new { x.OrderId, x.EventType }).IsUnique();
        b.HasIndex(x => new { x.NextAttemptAt, x.OccurredAt }).HasFilter("\"PublishedAt\" IS NULL");
        // Sem FK: a auditoria da entrega pode ter retenção independente de Pedido.
    }
}
