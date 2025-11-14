using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biblioteka.Migrations
{
    /// <inheritdoc />
    public partial class DodanieWypozyczen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReserved",
                table: "Ksiazki",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Wypozyczenia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    KsiazkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    DataWypozyczenia = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OczekiwanaDataZwrotu = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FaktycznaDataZwrotu = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Przedluzono = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wypozyczenia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Wypozyczenia_Ksiazki_KsiazkaId",
                        column: x => x.KsiazkaId,
                        principalTable: "Ksiazki",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Wypozyczenia_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Wypozyczenia_KsiazkaId",
                table: "Wypozyczenia",
                column: "KsiazkaId");

            migrationBuilder.CreateIndex(
                name: "IX_Wypozyczenia_UserId",
                table: "Wypozyczenia",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Wypozyczenia");

            migrationBuilder.DropColumn(
                name: "IsReserved",
                table: "Ksiazki");
        }
    }
}
