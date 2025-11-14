using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biblioteka.Migrations
{
    /// <inheritdoc />
    public partial class KategoriaStruct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Kategorie",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nazwa = table.Column<string>(type: "TEXT", nullable: false),
                    ParentId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kategorie", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kategorie_Kategorie_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Kategorie",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Imie = table.Column<string>(type: "TEXT", nullable: false),
                    Nazwisko = table.Column<string>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", nullable: false),
                    haslo = table.Column<string>(type: "TEXT", nullable: false),
                    dataRejestracji = table.Column<DateTime>(type: "TEXT", nullable: false),
                    iloscWypKsiazek = table.Column<int>(type: "INTEGER", nullable: false),
                    Rola = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ksiazki",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    isbn = table.Column<int>(type: "INTEGER", nullable: false),
                    tytul = table.Column<string>(type: "TEXT", nullable: false),
                    autor = table.Column<string>(type: "TEXT", nullable: false),
                    tag = table.Column<string>(type: "TEXT", nullable: false),
                    stan = table.Column<string>(type: "TEXT", nullable: false),
                    KategoriaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ksiazki", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ksiazki_Kategorie_KategoriaId",
                        column: x => x.KategoriaId,
                        principalTable: "Kategorie",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kategorie_ParentId",
                table: "Kategorie",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Ksiazki_KategoriaId",
                table: "Ksiazki",
                column: "KategoriaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ksiazki");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Kategorie");
        }
    }
}
