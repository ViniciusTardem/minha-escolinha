using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaEscolinha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SessaoRemarcada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RemarcadaDe_ID",
                table: "SessaoTreino",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemarcadaPara_ID",
                table: "SessaoTreino",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessaoTreino_RemarcadaDe",
                table: "SessaoTreino",
                column: "RemarcadaDe_ID",
                filter: "[RemarcadaDe_ID] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_SessaoTreino_RemarcadaDe",
                table: "SessaoTreino",
                column: "RemarcadaDe_ID",
                principalTable: "SessaoTreino",
                principalColumn: "SessaoTreino_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessaoTreino_RemarcadaDe",
                table: "SessaoTreino");

            migrationBuilder.DropIndex(
                name: "IX_SessaoTreino_RemarcadaDe",
                table: "SessaoTreino");

            migrationBuilder.DropColumn(
                name: "RemarcadaDe_ID",
                table: "SessaoTreino");

            migrationBuilder.DropColumn(
                name: "RemarcadaPara_ID",
                table: "SessaoTreino");
        }
    }
}
