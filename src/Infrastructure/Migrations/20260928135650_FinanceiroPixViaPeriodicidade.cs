using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaEscolinha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinanceiroPixViaPeriodicidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Cobranca_Escolinha_Matricula_Competencia",
                table: "Cobranca");

            migrationBuilder.AddColumn<int>(
                name: "PeriodicidadeMeses",
                table: "Turma",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "CobradaDeNovoDe_ID",
                table: "Cobranca",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PixImagem",
                table: "Cobranca",
                type: "varchar(max)",
                unicode: false,
                maxLength: 20000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Via",
                table: "Cobranca",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Turma_Periodicidade",
                table: "Turma",
                sql: "[PeriodicidadeMeses] IN (1, 3, 6, 12)");

            migrationBuilder.CreateIndex(
                name: "IX_Cobranca_CobradaDeNovoDe_ID",
                table: "Cobranca",
                column: "CobradaDeNovoDe_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_Cobranca_Escolinha_Matricula_Competencia_Via",
                table: "Cobranca",
                columns: new[] { "Escolinha_ID", "Matricula_ID", "Competencia", "Via" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cobranca_CobradaDeNovoDe",
                table: "Cobranca",
                column: "CobradaDeNovoDe_ID",
                principalTable: "Cobranca",
                principalColumn: "Cobranca_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cobranca_CobradaDeNovoDe",
                table: "Cobranca");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Turma_Periodicidade",
                table: "Turma");

            migrationBuilder.DropIndex(
                name: "IX_Cobranca_CobradaDeNovoDe_ID",
                table: "Cobranca");

            migrationBuilder.DropIndex(
                name: "UQ_Cobranca_Escolinha_Matricula_Competencia_Via",
                table: "Cobranca");

            migrationBuilder.DropColumn(
                name: "PeriodicidadeMeses",
                table: "Turma");

            migrationBuilder.DropColumn(
                name: "CobradaDeNovoDe_ID",
                table: "Cobranca");

            migrationBuilder.DropColumn(
                name: "PixImagem",
                table: "Cobranca");

            migrationBuilder.DropColumn(
                name: "Via",
                table: "Cobranca");

            migrationBuilder.CreateIndex(
                name: "UQ_Cobranca_Escolinha_Matricula_Competencia",
                table: "Cobranca",
                columns: new[] { "Escolinha_ID", "Matricula_ID", "Competencia" },
                unique: true);
        }
    }
}
