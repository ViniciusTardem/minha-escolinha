using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaEscolinha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PresencaS3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertaFaltas",
                columns: table => new
                {
                    AlertaFaltas_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Aluno_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Turma_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessaoTreino_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessaoInicio = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Regra = table.Column<int>(type: "int", nullable: false),
                    Limite = table.Column<int>(type: "int", nullable: false),
                    ContaJustificada = table.Column<bool>(type: "bit", nullable: false),
                    Faltas = table.Column<int>(type: "int", nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AvaliadoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    AnuladoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertaFaltas", x => x.AlertaFaltas_ID);
                    table.CheckConstraint("CK_AlertaFaltas_Faltas", "[Faltas] >= 1 AND [Limite] >= 1");
                    table.CheckConstraint("CK_AlertaFaltas_Situacao", "[Situacao] IN (10, 20, 30)");
                    table.ForeignKey(
                        name: "FK_AlertaFaltas_Aluno",
                        column: x => x.Aluno_ID,
                        principalTable: "Aluno",
                        principalColumn: "Aluno_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AlertaFaltas_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AlertaFaltas_SessaoTreino",
                        column: x => x.SessaoTreino_ID,
                        principalTable: "SessaoTreino",
                        principalColumn: "SessaoTreino_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AlertaFaltas_Turma",
                        column: x => x.Turma_ID,
                        principalTable: "Turma",
                        principalColumn: "Turma_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarcacaoPresenca",
                columns: table => new
                {
                    MarcacaoPresenca_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Marcacao = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessaoTreino_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Aluno_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    Justificativa = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    JustificativaSaude = table.Column<bool>(type: "bit", nullable: false),
                    Dispositivo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Sequencia = table.Column<long>(type: "bigint", nullable: false),
                    MarcadaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RecebidaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Pessoa_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PessoaNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Resultado = table.Column<int>(type: "int", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarcacaoPresenca", x => x.MarcacaoPresenca_ID);
                    table.CheckConstraint("CK_MarcacaoPresenca_Resultado", "[Resultado] IN (10, 20)");
                    table.ForeignKey(
                        name: "FK_MarcacaoPresenca_Aluno",
                        column: x => x.Aluno_ID,
                        principalTable: "Aluno",
                        principalColumn: "Aluno_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarcacaoPresenca_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarcacaoPresenca_SessaoTreino",
                        column: x => x.SessaoTreino_ID,
                        principalTable: "SessaoTreino",
                        principalColumn: "SessaoTreino_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Presenca",
                columns: table => new
                {
                    Presenca_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessaoTreino_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Aluno_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Turma_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessaoInicio = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    Justificativa = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    JustificativaSaude = table.Column<bool>(type: "bit", nullable: false),
                    Dispositivo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Sequencia = table.Column<long>(type: "bigint", nullable: false),
                    Marcacao = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarcadaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    MarcadaPor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MarcadaPorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Presenca", x => x.Presenca_ID);
                    table.CheckConstraint("CK_Presenca_Situacao", "[Situacao] IN (10, 20, 30)");
                    table.ForeignKey(
                        name: "FK_Presenca_Aluno",
                        column: x => x.Aluno_ID,
                        principalTable: "Aluno",
                        principalColumn: "Aluno_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Presenca_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Presenca_SessaoTreino",
                        column: x => x.SessaoTreino_ID,
                        principalTable: "SessaoTreino",
                        principalColumn: "SessaoTreino_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Presenca_Turma",
                        column: x => x.Turma_ID,
                        principalTable: "Turma",
                        principalColumn: "Turma_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertaFaltas_Aluno_ID",
                table: "AlertaFaltas",
                column: "Aluno_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AlertaFaltas_Escolinha_Aluno_Situacao_Avaliado",
                table: "AlertaFaltas",
                columns: new[] { "Escolinha_ID", "Aluno_ID", "Situacao", "AvaliadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertaFaltas_SessaoTreino_ID",
                table: "AlertaFaltas",
                column: "SessaoTreino_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AlertaFaltas_Turma_ID",
                table: "AlertaFaltas",
                column: "Turma_ID");

            migrationBuilder.CreateIndex(
                name: "IX_MarcacaoPresenca_Aluno_ID",
                table: "MarcacaoPresenca",
                column: "Aluno_ID");

            migrationBuilder.CreateIndex(
                name: "IX_MarcacaoPresenca_Escolinha_Sessao_Aluno",
                table: "MarcacaoPresenca",
                columns: new[] { "Escolinha_ID", "SessaoTreino_ID", "Aluno_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_MarcacaoPresenca_SessaoTreino_ID",
                table: "MarcacaoPresenca",
                column: "SessaoTreino_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_MarcacaoPresenca_Escolinha_Marcacao",
                table: "MarcacaoPresenca",
                columns: new[] { "Escolinha_ID", "Marcacao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Presenca_Aluno_ID",
                table: "Presenca",
                column: "Aluno_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Presenca_Escolinha_Aluno_Turma_Inicio",
                table: "Presenca",
                columns: new[] { "Escolinha_ID", "Aluno_ID", "Turma_ID", "SessaoInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Presenca_Escolinha_Inicio",
                table: "Presenca",
                columns: new[] { "Escolinha_ID", "SessaoInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Presenca_SessaoTreino_ID",
                table: "Presenca",
                column: "SessaoTreino_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Presenca_Turma_ID",
                table: "Presenca",
                column: "Turma_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_Presenca_Escolinha_Sessao_Aluno",
                table: "Presenca",
                columns: new[] { "Escolinha_ID", "SessaoTreino_ID", "Aluno_ID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertaFaltas");

            migrationBuilder.DropTable(
                name: "MarcacaoPresenca");

            migrationBuilder.DropTable(
                name: "Presenca");
        }
    }
}
