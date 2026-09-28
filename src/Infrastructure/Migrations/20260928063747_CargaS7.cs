using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaEscolinha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CargaS7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportacaoLote",
                columns: table => new
                {
                    ImportacaoLote_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomeArquivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Linhas = table.Column<int>(type: "int", nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    Conteudo = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true),
                    Mapeamento = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false),
                    Decisoes = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false),
                    EnviadoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    EnviadoPor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EnviadoPorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EncerradoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    EncerradoPor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EncerradoPorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AlunosCriados = table.Column<int>(type: "int", nullable: false),
                    ResponsaveisCriados = table.Column<int>(type: "int", nullable: false),
                    ResponsaveisReaproveitados = table.Column<int>(type: "int", nullable: false),
                    LinhasNaoImportadas = table.Column<int>(type: "int", nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportacaoLote", x => x.ImportacaoLote_ID);
                    table.CheckConstraint("CK_ImportacaoLote_Conteudo", "[Situacao] = 10 OR [Conteudo] IS NULL");
                    table.CheckConstraint("CK_ImportacaoLote_Situacao", "[Situacao] IN (10, 20, 30)");
                    table.ForeignKey(
                        name: "FK_ImportacaoLote_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportacaoLote_Escolinha_EnviadoEm",
                table: "ImportacaoLote",
                columns: new[] { "Escolinha_ID", "EnviadoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportacaoLote");
        }
    }
}
