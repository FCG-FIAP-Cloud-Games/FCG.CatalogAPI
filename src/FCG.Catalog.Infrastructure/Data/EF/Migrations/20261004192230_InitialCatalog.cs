using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCG.Catalog.Infrastructure.Data.EF.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "jogos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    faixa_etaria = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    preco = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    data_cadastro = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jogos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tb_Categorias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_Categorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rel_CategoriaJogo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JogoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoriaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rel_CategoriaJogo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rel_CategoriaJogo_jogos_JogoId",
                        column: x => x.JogoId,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rel_CategoriaJogo_tb_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "tb_Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rel_CategoriaJogo_CategoriaId",
                table: "rel_CategoriaJogo",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_rel_CategoriaJogo_JogoId_CategoriaId",
                table: "rel_CategoriaJogo",
                columns: new[] { "JogoId", "CategoriaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rel_CategoriaJogo");

            migrationBuilder.DropTable(
                name: "jogos");

            migrationBuilder.DropTable(
                name: "tb_Categorias");
        }
    }
}
