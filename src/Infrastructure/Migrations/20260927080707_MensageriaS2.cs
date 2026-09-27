using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaEscolinha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MensageriaS2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoRecusa",
                table: "VinculoAlunoResponsavel",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Verificacao",
                table: "VinculoAlunoResponsavel",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerificacaoSolicitadaEm",
                table: "VinculoAlunoResponsavel",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerificadoEm",
                table: "VinculoAlunoResponsavel",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificadoPorNome",
                table: "VinculoAlunoResponsavel",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VerificadoPor_ID",
                table: "VinculoAlunoResponsavel",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConviteAcesso",
                columns: table => new
                {
                    ConviteAcesso_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Responsavel_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Professor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Telefone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    TokenHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    EmitidoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    EmitidoPor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmitidoPorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TentativasIdentificacao = table.Column<int>(type: "int", nullable: false),
                    AceitoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Pessoa_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EncerradoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    MotivoEncerramento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConviteAcesso", x => x.ConviteAcesso_ID);
                    table.CheckConstraint("CK_ConviteAcesso_Ficha", "([Tipo] = 10 AND [Responsavel_ID] IS NOT NULL AND [Professor_ID] IS NULL) OR ([Tipo] = 20 AND [Professor_ID] IS NOT NULL AND [Responsavel_ID] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ConviteAcesso_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConviteAcesso_Pessoa",
                        column: x => x.Pessoa_ID,
                        principalTable: "Pessoa",
                        principalColumn: "Pessoa_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConviteAcesso_Professor",
                        column: x => x.Professor_ID,
                        principalTable: "Professor",
                        principalColumn: "Professor_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConviteAcesso_Responsavel",
                        column: x => x.Responsavel_ID,
                        principalTable: "Responsavel",
                        principalColumn: "Responsavel_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Mensagem",
                columns: table => new
                {
                    Mensagem_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Gatilho = table.Column<int>(type: "int", nullable: false),
                    Canal = table.Column<int>(type: "int", nullable: false),
                    Chave = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    TipoOrigem = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Origem_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoDestinatario = table.Column<int>(type: "int", nullable: false),
                    Destinatario_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinatarioNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Telefone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Pessoa_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Aluno_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Template = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    Variaveis = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Conteudo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Essencial = table.Column<bool>(type: "bit", nullable: false),
                    Urgente = table.Column<bool>(type: "bit", nullable: false),
                    Referencia = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CriadaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    AgendadaPara = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    EnviadaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    EntregueEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    LidaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    FalhouEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RespondidaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IdExterno = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    Tentativas = table.Column<int>(type: "int", nullable: false),
                    CreditoReservado = table.Column<bool>(type: "bit", nullable: false),
                    CreditosConsumidos = table.Column<int>(type: "int", nullable: false),
                    ReservadaAte = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mensagem", x => x.Mensagem_ID);
                    table.CheckConstraint("CK_Mensagem_Canal", "[Canal] IN (10, 20)");
                    table.CheckConstraint("CK_Mensagem_Creditos", "[CreditosConsumidos] >= 0");
                    table.CheckConstraint("CK_Mensagem_TipoDestinatario", "[TipoDestinatario] IN (10, 20)");
                    table.ForeignKey(
                        name: "FK_Mensagem_Aluno",
                        column: x => x.Aluno_ID,
                        principalTable: "Aluno",
                        principalColumn: "Aluno_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mensagem_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mensagem_Pessoa",
                        column: x => x.Pessoa_ID,
                        principalTable: "Pessoa",
                        principalColumn: "Pessoa_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RastreioWhatsApp",
                columns: table => new
                {
                    RastreioWhatsApp_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    IdExterno = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    ChaveTelefone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mensagem_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnviadoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RastreioWhatsApp", x => x.RastreioWhatsApp_ID);
                });

            migrationBuilder.CreateTable(
                name: "TemplateMensagem",
                columns: table => new
                {
                    TemplateMensagem_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Gatilho = table.Column<int>(type: "int", nullable: false),
                    Tom = table.Column<int>(type: "int", nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    NomeNaMeta = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    Idioma = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Corpo = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Variaveis = table.Column<string>(type: "varchar(300)", unicode: false, maxLength: 300, nullable: false),
                    Botoes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateMensagem", x => x.TemplateMensagem_ID);
                });

            migrationBuilder.CreateTable(
                name: "EventoMensagem",
                columns: table => new
                {
                    EventoMensagem_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mensagem_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Em = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Detalhe = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoMensagem", x => x.EventoMensagem_ID);
                    table.ForeignKey(
                        name: "FK_EventoMensagem_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventoMensagem_Mensagem",
                        column: x => x.Mensagem_ID,
                        principalTable: "Mensagem",
                        principalColumn: "Mensagem_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RespostaMensagem",
                columns: table => new
                {
                    RespostaMensagem_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mensagem_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Canal = table.Column<int>(type: "int", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Payload = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    IdExterno = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    RecebidaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Tratada = table.Column<bool>(type: "bit", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RespostaMensagem", x => x.RespostaMensagem_ID);
                    table.ForeignKey(
                        name: "FK_RespostaMensagem_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RespostaMensagem_Mensagem",
                        column: x => x.Mensagem_ID,
                        principalTable: "Mensagem",
                        principalColumn: "Mensagem_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VinculoAlunoResponsavel_Escolinha_Verificacao",
                table: "VinculoAlunoResponsavel",
                columns: new[] { "Escolinha_ID", "Verificacao" });

            migrationBuilder.CreateIndex(
                name: "IX_ConviteAcesso_Escolinha_Professor",
                table: "ConviteAcesso",
                columns: new[] { "Escolinha_ID", "Professor_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_ConviteAcesso_Escolinha_Responsavel",
                table: "ConviteAcesso",
                columns: new[] { "Escolinha_ID", "Responsavel_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_ConviteAcesso_Pessoa_ID",
                table: "ConviteAcesso",
                column: "Pessoa_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ConviteAcesso_Professor_ID",
                table: "ConviteAcesso",
                column: "Professor_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ConviteAcesso_Responsavel_ID",
                table: "ConviteAcesso",
                column: "Responsavel_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ConviteAcesso_TokenHash",
                table: "ConviteAcesso",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "UQ_ConviteAcesso_Escolinha_TokenHash",
                table: "ConviteAcesso",
                columns: new[] { "Escolinha_ID", "TokenHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventoMensagem_Escolinha_Mensagem",
                table: "EventoMensagem",
                columns: new[] { "Escolinha_ID", "Mensagem_ID", "Em" });

            migrationBuilder.CreateIndex(
                name: "IX_EventoMensagem_Mensagem_ID",
                table: "EventoMensagem",
                column: "Mensagem_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Mensagem_Aluno_ID",
                table: "Mensagem",
                column: "Aluno_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Mensagem_Escolinha_Aluno",
                table: "Mensagem",
                columns: new[] { "Escolinha_ID", "Aluno_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensagem_Escolinha_CriadaEm",
                table: "Mensagem",
                columns: new[] { "Escolinha_ID", "CriadaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensagem_Escolinha_Fila",
                table: "Mensagem",
                columns: new[] { "Escolinha_ID", "Canal", "Situacao", "AgendadaPara" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensagem_Escolinha_Origem",
                table: "Mensagem",
                columns: new[] { "Escolinha_ID", "TipoOrigem", "Origem_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensagem_Escolinha_Pessoa",
                table: "Mensagem",
                columns: new[] { "Escolinha_ID", "Pessoa_ID", "Canal" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensagem_Pessoa_ID",
                table: "Mensagem",
                column: "Pessoa_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_Mensagem_Escolinha_Chave",
                table: "Mensagem",
                columns: new[] { "Escolinha_ID", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RastreioWhatsApp_Telefone_EnviadoEm",
                table: "RastreioWhatsApp",
                columns: new[] { "ChaveTelefone", "EnviadoEm" });

            migrationBuilder.CreateIndex(
                name: "UQ_RastreioWhatsApp_IdExterno",
                table: "RastreioWhatsApp",
                column: "IdExterno",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RespostaMensagem_Escolinha_Mensagem",
                table: "RespostaMensagem",
                columns: new[] { "Escolinha_ID", "Mensagem_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_RespostaMensagem_Mensagem_ID",
                table: "RespostaMensagem",
                column: "Mensagem_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_TemplateMensagem_Gatilho_Tom",
                table: "TemplateMensagem",
                columns: new[] { "Gatilho", "Tom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_TemplateMensagem_NomeNaMeta",
                table: "TemplateMensagem",
                column: "NomeNaMeta",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConviteAcesso");

            migrationBuilder.DropTable(
                name: "EventoMensagem");

            migrationBuilder.DropTable(
                name: "RastreioWhatsApp");

            migrationBuilder.DropTable(
                name: "RespostaMensagem");

            migrationBuilder.DropTable(
                name: "TemplateMensagem");

            migrationBuilder.DropTable(
                name: "Mensagem");

            migrationBuilder.DropIndex(
                name: "IX_VinculoAlunoResponsavel_Escolinha_Verificacao",
                table: "VinculoAlunoResponsavel");

            migrationBuilder.DropColumn(
                name: "MotivoRecusa",
                table: "VinculoAlunoResponsavel");

            migrationBuilder.DropColumn(
                name: "Verificacao",
                table: "VinculoAlunoResponsavel");

            migrationBuilder.DropColumn(
                name: "VerificacaoSolicitadaEm",
                table: "VinculoAlunoResponsavel");

            migrationBuilder.DropColumn(
                name: "VerificadoEm",
                table: "VinculoAlunoResponsavel");

            migrationBuilder.DropColumn(
                name: "VerificadoPorNome",
                table: "VinculoAlunoResponsavel");

            migrationBuilder.DropColumn(
                name: "VerificadoPor_ID",
                table: "VinculoAlunoResponsavel");
        }
    }
}
