using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCG.Catalog.Infrastructure.Data.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddPedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pedidos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pedidos_jogos_GameId",
                        column: x => x.GameId,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_GameId",
                table: "pedidos",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "ux_pedidos_usuario_chave",
                table: "pedidos",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pedidos_usuario_jogo_pendente",
                table: "pedidos",
                columns: new[] { "UserId", "GameId" },
                unique: true,
                filter: "\"Status\" = 'PendingPayment'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pedidos");
        }
    }
}
