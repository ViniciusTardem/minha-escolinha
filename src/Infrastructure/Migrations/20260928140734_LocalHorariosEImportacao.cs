using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaEscolinha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LocalHorariosEImportacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FaixaHorarioLocal",
                columns: table => new
                {
                    FaixaHorarioLocal_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Local_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiaSemana = table.Column<int>(type: "int", nullable: false),
                    Inicio = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    Fim = table.Column<TimeOnly>(type: "time(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaixaHorarioLocal", x => x.FaixaHorarioLocal_ID);
                    table.CheckConstraint("CK_FaixaHorarioLocal_Horario", "[Fim] > [Inicio]");
                    table.ForeignKey(
                        name: "FK_FaixaHorarioLocal_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FaixaHorarioLocal_Local",
                        column: x => x.Local_ID,
                        principalTable: "Local",
                        principalColumn: "Local_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FaixaHorarioLocal_Escolinha_Local",
                table: "FaixaHorarioLocal",
                columns: new[] { "Escolinha_ID", "Local_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_FaixaHorarioLocal_Local_ID",
                table: "FaixaHorarioLocal",
                column: "Local_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FaixaHorarioLocal");
        }
    }
}
