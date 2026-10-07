using FCG.Catalog.Infrastructure.Inbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Catalog.Infrastructure.Data.EF.Mappings;

internal sealed class MensagemInboxMapping : IEntityTypeConfiguration<MensagemInbox>
{
    public void Configure(EntityTypeBuilder<MensagemInbox> b)
    {
        b.ToTable("inbox_messages");
        b.HasKey(m => m.Id).HasName("pk_inbox_messages");
        b.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(m => m.EventId).HasColumnName("event_id").IsRequired();
        b.Property(m => m.ConsumerName).HasColumnName("consumer_name").HasMaxLength(100).IsRequired();
        b.Property(m => m.ProcessedAt).HasColumnName("processed_at").HasColumnType("timestamp with time zone").IsRequired();
        b.HasIndex(m => new { m.ConsumerName, m.EventId }).IsUnique().HasDatabaseName("ux_inbox_consumer_event");
    }
}
