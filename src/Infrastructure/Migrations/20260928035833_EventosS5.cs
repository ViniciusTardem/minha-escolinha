using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaEscolinha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EventosS5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Evento",
                columns: table => new
                {
                    Evento_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Modalidade_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Inicio = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    DuracaoMinutos = table.Column<int>(type: "int", nullable: false),
                    Local_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Professor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Adversario = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Logistica = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EncerradoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    EncerradoPorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evento", x => x.Evento_ID);
                    table.CheckConstraint("CK_Evento_Duracao", "[DuracaoMinutos] BETWEEN 15 AND 600");
                    table.CheckConstraint("CK_Evento_Situacao", "[Situacao] IN (10, 20, 30, 40, 50, 60)");
                    table.CheckConstraint("CK_Evento_Tipo", "[Tipo] IN (10)");
                    table.ForeignKey(
                        name: "FK_Evento_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evento_Local",
                        column: x => x.Local_ID,
                        principalTable: "Local",
                        principalColumn: "Local_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evento_Modalidade",
                        column: x => x.Modalidade_ID,
                        principalTable: "Modalidade",
                        principalColumn: "Modalidade_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evento_Professor",
                        column: x => x.Professor_ID,
                        principalTable: "Professor",
                        principalColumn: "Professor_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Convocacao",
                columns: table => new
                {
                    Convocacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Evento_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Aluno_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Turma_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    EnviadaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    LembreteEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RespondidaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: true),
                    RespondidoPorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    RespondidoPor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OutraResposta = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Convocacao", x => x.Convocacao_ID);
                    table.CheckConstraint("CK_Convocacao_Origem", "[Origem] IS NULL OR [Origem] IN (10, 20, 30)");
                    table.CheckConstraint("CK_Convocacao_Situacao", "[Situacao] IN (5, 10, 20, 30, 40, 50)");
                    table.ForeignKey(
                        name: "FK_Convocacao_Aluno",
                        column: x => x.Aluno_ID,
                        principalTable: "Aluno",
                        principalColumn: "Aluno_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Convocacao_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Convocacao_Evento",
                        column: x => x.Evento_ID,
                        principalTable: "Evento",
                        principalColumn: "Evento_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Convocacao_Turma",
                        column: x => x.Turma_ID,
                        principalTable: "Turma",
                        principalColumn: "Turma_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventoTurma",
                columns: table => new
                {
                    EventoTurma_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Evento_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Turma_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoTurma", x => x.EventoTurma_ID);
                    table.ForeignKey(
                        name: "FK_EventoTurma_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventoTurma_Evento",
                        column: x => x.Evento_ID,
                        principalTable: "Evento",
                        principalColumn: "Evento_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventoTurma_Turma",
                        column: x => x.Turma_ID,
                        principalTable: "Turma",
                        principalColumn: "Turma_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistoricoConvocacao",
                columns: table => new
                {
                    HistoricoConvocacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Convocacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Em = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Detalhe = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricoConvocacao", x => x.HistoricoConvocacao_ID);
                    table.ForeignKey(
                        name: "FK_HistoricoConvocacao_Convocacao",
                        column: x => x.Convocacao_ID,
                        principalTable: "Convocacao",
                        principalColumn: "Convocacao_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistoricoConvocacao_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Convocacao_Aluno_ID",
                table: "Convocacao",
                column: "Aluno_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Convocacao_Escolinha_Aluno",
                table: "Convocacao",
                columns: new[] { "Escolinha_ID", "Aluno_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_Convocacao_Evento_ID",
                table: "Convocacao",
                column: "Evento_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Convocacao_Turma_ID",
                table: "Convocacao",
                column: "Turma_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_Convocacao_Escolinha_Evento_Aluno",
                table: "Convocacao",
                columns: new[] { "Escolinha_ID", "Evento_ID", "Aluno_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evento_Escolinha_Inicio",
                table: "Evento",
                columns: new[] { "Escolinha_ID", "Inicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Evento_Escolinha_Professor_Inicio",
                table: "Evento",
                columns: new[] { "Escolinha_ID", "Professor_ID", "Inicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Evento_Local_ID",
                table: "Evento",
                column: "Local_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Evento_Modalidade_ID",
                table: "Evento",
                column: "Modalidade_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Evento_Professor_ID",
                table: "Evento",
                column: "Professor_ID");

            migrationBuilder.CreateIndex(
                name: "IX_EventoTurma_Escolinha_Turma",
                table: "EventoTurma",
                columns: new[] { "Escolinha_ID", "Turma_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_EventoTurma_Evento_ID",
                table: "EventoTurma",
                column: "Evento_ID");

            migrationBuilder.CreateIndex(
                name: "IX_EventoTurma_Turma_ID",
                table: "EventoTurma",
                column: "Turma_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_EventoTurma_Escolinha_Evento_Turma",
                table: "EventoTurma",
                columns: new[] { "Escolinha_ID", "Evento_ID", "Turma_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoConvocacao_Convocacao_ID",
                table: "HistoricoConvocacao",
                column: "Convocacao_ID");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoConvocacao_Escolinha_Convocacao",
                table: "HistoricoConvocacao",
                columns: new[] { "Escolinha_ID", "Convocacao_ID", "Em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventoTurma");

            migrationBuilder.DropTable(
                name: "HistoricoConvocacao");

            migrationBuilder.DropTable(
                name: "Convocacao");

            migrationBuilder.DropTable(
                name: "Evento");
        }
    }
}
