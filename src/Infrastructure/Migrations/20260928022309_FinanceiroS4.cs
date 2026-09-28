using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaEscolinha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinanceiroS4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Escolinha_ID",
                table: "WebhookRecebido",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Etapa",
                table: "Mensagem",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Identificador",
                table: "ContaAsaasEscolinha",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()");

            migrationBuilder.AddColumn<string>(
                name: "TokenWebhookProtegido",
                table: "ContaAsaasEscolinha",
                type: "varchar(2000)",
                unicode: false,
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookErro",
                table: "ContaAsaasEscolinha",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WebhookRegistradoEm",
                table: "ContaAsaasEscolinha",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClienteAsaas",
                columns: table => new
                {
                    ClienteAsaas_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Responsavel_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Conta = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdAsaas = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClienteAsaas", x => x.ClienteAsaas_ID);
                    table.ForeignKey(
                        name: "FK_ClienteAsaas_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClienteAsaas_Responsavel",
                        column: x => x.Responsavel_ID,
                        principalTable: "Responsavel",
                        principalColumn: "Responsavel_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cobranca",
                columns: table => new
                {
                    Cobranca_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Aluno_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Matricula_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Turma_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Competencia = table.Column<DateOnly>(type: "date", nullable: false),
                    Vencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PrimeiraDaMatricula = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmadaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ConfirmadaPor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfirmadaPorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    Pagador_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PagadorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    MultaPercentual = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    JurosPercentualMes = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    IdAsaas = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    UrlFatura = table.Column<string>(type: "varchar(300)", unicode: false, maxLength: 300, nullable: true),
                    ContaAsaas = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmitidaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    TentativasEmissao = table.Column<int>(type: "int", nullable: false),
                    ErroEmissao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ErroEmissaoDefinitivo = table.Column<bool>(type: "bit", nullable: false),
                    ConferidaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Meio = table.Column<int>(type: "int", nullable: true),
                    ValorPago = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    PagaEm = table.Column<DateOnly>(type: "date", nullable: true),
                    ConciliadaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EncerradaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    EncerradaPor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EncerradaPorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    UltimaEtapa = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    UltimaEtapaData = table.Column<DateOnly>(type: "date", nullable: true),
                    Sinal = table.Column<int>(type: "int", nullable: false),
                    SinalDetalhe = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SinalEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    SinalResolucao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SinalResolvidoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    SinalResolvidoPorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cobranca", x => x.Cobranca_ID);
                    table.CheckConstraint("CK_Cobranca_Sinal", "[Sinal] IN (0, 10, 20, 30, 40, 50, 60)");
                    table.CheckConstraint("CK_Cobranca_Situacao", "[Situacao] IN (10, 20, 30, 40, 50, 60, 70)");
                    table.CheckConstraint("CK_Cobranca_Valor", "[Valor] > 0");
                    table.ForeignKey(
                        name: "FK_Cobranca_Aluno",
                        column: x => x.Aluno_ID,
                        principalTable: "Aluno",
                        principalColumn: "Aluno_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cobranca_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cobranca_Matricula",
                        column: x => x.Matricula_ID,
                        principalTable: "Matricula",
                        principalColumn: "Matricula_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cobranca_Pagador",
                        column: x => x.Pagador_ID,
                        principalTable: "Responsavel",
                        principalColumn: "Responsavel_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cobranca_Turma",
                        column: x => x.Turma_ID,
                        principalTable: "Turma",
                        principalColumn: "Turma_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClienteAsaas_Responsavel_ID",
                table: "ClienteAsaas",
                column: "Responsavel_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_ClienteAsaas_Escolinha_Responsavel_Conta",
                table: "ClienteAsaas",
                columns: new[] { "Escolinha_ID", "Responsavel_ID", "Conta" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cobranca_Aluno_ID",
                table: "Cobranca",
                column: "Aluno_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Cobranca_Escolinha_Aluno",
                table: "Cobranca",
                columns: new[] { "Escolinha_ID", "Aluno_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_Cobranca_Escolinha_Competencia",
                table: "Cobranca",
                columns: new[] { "Escolinha_ID", "Competencia" });

            migrationBuilder.CreateIndex(
                name: "IX_Cobranca_Escolinha_IdAsaas",
                table: "Cobranca",
                columns: new[] { "Escolinha_ID", "IdAsaas" },
                filter: "[IdAsaas] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cobranca_Escolinha_Sinal",
                table: "Cobranca",
                columns: new[] { "Escolinha_ID", "Sinal" },
                filter: "[Sinal] <> 0");

            migrationBuilder.CreateIndex(
                name: "IX_Cobranca_Escolinha_Situacao_Vencimento",
                table: "Cobranca",
                columns: new[] { "Escolinha_ID", "Situacao", "Vencimento" });

            migrationBuilder.CreateIndex(
                name: "IX_Cobranca_Matricula_ID",
                table: "Cobranca",
                column: "Matricula_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Cobranca_Pagador_ID",
                table: "Cobranca",
                column: "Pagador_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Cobranca_Turma_ID",
                table: "Cobranca",
                column: "Turma_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_Cobranca_Escolinha_Matricula_Competencia",
                table: "Cobranca",
                columns: new[] { "Escolinha_ID", "Matricula_ID", "Competencia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClienteAsaas");

            migrationBuilder.DropTable(
                name: "Cobranca");

            migrationBuilder.DropColumn(
                name: "Escolinha_ID",
                table: "WebhookRecebido");

            migrationBuilder.DropColumn(
                name: "Etapa",
                table: "Mensagem");

            migrationBuilder.DropColumn(
                name: "Identificador",
                table: "ContaAsaasEscolinha");

            migrationBuilder.DropColumn(
                name: "TokenWebhookProtegido",
                table: "ContaAsaasEscolinha");

            migrationBuilder.DropColumn(
                name: "WebhookErro",
                table: "ContaAsaasEscolinha");

            migrationBuilder.DropColumn(
                name: "WebhookRegistradoEm",
                table: "ContaAsaasEscolinha");
        }
    }
}
