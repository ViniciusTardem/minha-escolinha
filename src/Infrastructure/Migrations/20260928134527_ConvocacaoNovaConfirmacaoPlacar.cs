using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaEscolinha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvocacaoNovaConfirmacaoPlacar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlacarAdversario",
                table: "Evento",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlacarEm",
                table: "Evento",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlacarNos",
                table: "Evento",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlacarPorNome",
                table: "Evento",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NovaConfirmacaoEm",
                table: "Convocacao",
                type: "datetime2(3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlacarAdversario",
                table: "Evento");

            migrationBuilder.DropColumn(
                name: "PlacarEm",
                table: "Evento");

            migrationBuilder.DropColumn(
                name: "PlacarNos",
                table: "Evento");

            migrationBuilder.DropColumn(
                name: "PlacarPorNome",
                table: "Evento");

            migrationBuilder.DropColumn(
                name: "NovaConfirmacaoEm",
                table: "Convocacao");
        }
    }
}
