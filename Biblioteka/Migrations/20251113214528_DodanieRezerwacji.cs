using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biblioteka.Migrations
{
    /// <inheritdoc />
    public partial class DodanieRezerwacji : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReserved",
                table: "Ksiazki");

            migrationBuilder.CreateTable(
                name: "Rezerwacje",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    KsiazkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    DataRezerwacji = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataWygasniecia = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rezerwacje", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rezerwacje_Ksiazki_KsiazkaId",
                        column: x => x.KsiazkaId,
                        principalTable: "Ksiazki",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rezerwacje_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rezerwacje_KsiazkaId",
                table: "Rezerwacje",
                column: "KsiazkaId");

            migrationBuilder.CreateIndex(
                name: "IX_Rezerwacje_UserId",
                table: "Rezerwacje",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Rezerwacje");

            migrationBuilder.AddColumn<bool>(
                name: "IsReserved",
                table: "Ksiazki",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
