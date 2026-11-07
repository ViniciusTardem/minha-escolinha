using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaEscolinha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TermosPeloLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Mensagem_Canal",
                table: "Mensagem");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Mensagem",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PedidoTermos",
                columns: table => new
                {
                    PedidoTermos_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Aluno_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Responsavel_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    TokenProtegido = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: true),
                    PedeDados = table.Column<bool>(type: "bit", nullable: false),
                    PedeImagem = table.Column<bool>(type: "bit", nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CriadoPorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EnviadoPara = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    RespondidoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    AceitouDados = table.Column<bool>(type: "bit", nullable: true),
                    AceitouImagem = table.Column<bool>(type: "bit", nullable: true),
                    RespondidoDoIp = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    CanceladoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidoTermos", x => x.PedidoTermos_ID);
                    table.ForeignKey(
                        name: "FK_PedidoTermos_Aluno",
                        column: x => x.Aluno_ID,
                        principalTable: "Aluno",
                        principalColumn: "Aluno_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PedidoTermos_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PedidoTermos_Responsavel",
                        column: x => x.Responsavel_ID,
                        principalTable: "Responsavel",
                        principalColumn: "Responsavel_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Mensagem_Canal",
                table: "Mensagem",
                sql: "[Canal] IN (10, 20, 30)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Mensagem_Email",
                table: "Mensagem",
                sql: "[Canal] <> 30 OR [Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoTermos_Aluno_ID",
                table: "PedidoTermos",
                column: "Aluno_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoTermos_Escolinha_Aluno_CriadoEm",
                table: "PedidoTermos",
                columns: new[] { "Escolinha_ID", "Aluno_ID", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_PedidoTermos_Responsavel_ID",
                table: "PedidoTermos",
                column: "Responsavel_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoTermos_TokenHash",
                table: "PedidoTermos",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "UQ_PedidoTermos_Escolinha_TokenHash",
                table: "PedidoTermos",
                columns: new[] { "Escolinha_ID", "TokenHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PedidoTermos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Mensagem_Canal",
                table: "Mensagem");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Mensagem_Email",
                table: "Mensagem");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Mensagem");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Mensagem_Canal",
                table: "Mensagem",
                sql: "[Canal] IN (10, 20)");
        }
    }
}
