using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaEscolinha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComprasCreditosStripe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PacoteCreditos",
                columns: table => new
                {
                    PacoteCreditos_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Creditos = table.Column<int>(type: "int", nullable: false),
                    Preco = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PacoteCreditos", x => x.PacoteCreditos_ID);
                    table.CheckConstraint("CK_PacoteCreditos_Creditos", "[Creditos] > 0");
                    table.CheckConstraint("CK_PacoteCreditos_Preco", "[Preco] >= 1");
                });

            migrationBuilder.CreateTable(
                name: "WebhookRecebido",
                columns: table => new
                {
                    WebhookRecebido_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Origem = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    EventoExterno = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    Tipo = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Corpo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ip = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    RecebidoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    ProcessadoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Tentativas = table.Column<int>(type: "int", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookRecebido", x => x.WebhookRecebido_ID);
                });

            migrationBuilder.CreateTable(
                name: "CompraCreditos",
                columns: table => new
                {
                    CompraCreditos_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PacoteCreditos_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomePacote = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Creditos = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    SessaoCheckoutId = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    PagamentoId = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    MeioPagamento = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    IniciadaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ConcluidaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Erro = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Autor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AutorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompraCreditos", x => x.CompraCreditos_ID);
                    table.CheckConstraint("CK_CompraCreditos_Creditos", "[Creditos] > 0");
                    table.CheckConstraint("CK_CompraCreditos_Valor", "[Valor] > 0");
                    table.ForeignKey(
                        name: "FK_CompraCreditos_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompraCreditos_PacoteCreditos",
                        column: x => x.PacoteCreditos_ID,
                        principalTable: "PacoteCreditos",
                        principalColumn: "PacoteCreditos_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompraCreditos_Escolinha_IniciadaEm",
                table: "CompraCreditos",
                columns: new[] { "Escolinha_ID", "IniciadaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_CompraCreditos_Escolinha_Situacao",
                table: "CompraCreditos",
                columns: new[] { "Escolinha_ID", "Situacao" });

            migrationBuilder.CreateIndex(
                name: "IX_CompraCreditos_PacoteCreditos_ID",
                table: "CompraCreditos",
                column: "PacoteCreditos_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_CompraCreditos_SessaoCheckout",
                table: "CompraCreditos",
                column: "SessaoCheckoutId",
                unique: true,
                filter: "[SessaoCheckoutId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PacoteCreditos_Ativo_Ordem",
                table: "PacoteCreditos",
                columns: new[] { "Ativo", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookRecebido_Origem_RecebidoEm",
                table: "WebhookRecebido",
                columns: new[] { "Origem", "RecebidoEm" });

            migrationBuilder.CreateIndex(
                name: "UQ_WebhookRecebido_Origem_Evento",
                table: "WebhookRecebido",
                columns: new[] { "Origem", "EventoExterno" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompraCreditos");

            migrationBuilder.DropTable(
                name: "WebhookRecebido");

            migrationBuilder.DropTable(
                name: "PacoteCreditos");
        }
    }
}
