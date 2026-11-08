using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCG.Catalog.Infrastructure.Data.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockToken = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_outbox", x => x.Id);
                    table.CheckConstraint("ck_outbox_attempts", "\"Attempts\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_outbox_EventId",
                table: "catalog_outbox",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_outbox_NextAttemptAt_OccurredAt",
                table: "catalog_outbox",
                columns: new[] { "NextAttemptAt", "OccurredAt" },
                filter: "\"PublishedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_outbox_OrderId_EventType",
                table: "catalog_outbox",
                columns: new[] { "OrderId", "EventType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_outbox");
        }
    }
}
