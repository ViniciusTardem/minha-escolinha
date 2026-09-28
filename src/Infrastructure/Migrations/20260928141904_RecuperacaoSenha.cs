using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaEscolinha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecuperacaoSenha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecuperacaoSenha",
                columns: table => new
                {
                    RecuperacaoSenha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Pessoa_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Canal = table.Column<int>(type: "int", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsadaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CriadaPorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TokenProtegido = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: true),
                    EmailEnviadoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecuperacaoSenha", x => x.RecuperacaoSenha_ID);
                    table.ForeignKey(
                        name: "FK_RecuperacaoSenha_Pessoa",
                        column: x => x.Pessoa_ID,
                        principalTable: "Pessoa",
                        principalColumn: "Pessoa_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecuperacaoSenha_Pessoa_CriadaEm",
                table: "RecuperacaoSenha",
                columns: new[] { "Pessoa_ID", "CriadaEm" });

            migrationBuilder.CreateIndex(
                name: "UQ_RecuperacaoSenha_TokenHash",
                table: "RecuperacaoSenha",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecuperacaoSenha");
        }
    }
}
