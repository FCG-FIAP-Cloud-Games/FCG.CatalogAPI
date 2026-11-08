using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCG.Catalog.Infrastructure.Data.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddAquisicoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "aquisicoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    jogo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_aquisicao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aquisicoes", x => x.id);
                    table.ForeignKey(
                        name: "fk_aquisicoes_jogos",
                        column: x => x.jogo_id,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_aquisicoes_pedidos",
                        column: x => x.pedido_id,
                        principalTable: "pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_aquisicoes_jogo",
                table: "aquisicoes",
                column: "jogo_id");

            migrationBuilder.CreateIndex(
                name: "ux_aquisicoes_pedido",
                table: "aquisicoes",
                column: "pedido_id",
                unique: true,
                filter: "pedido_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_aquisicoes_usuario_jogo",
                table: "aquisicoes",
                columns: new[] { "usuario_id", "jogo_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aquisicoes");
        }
    }
}
