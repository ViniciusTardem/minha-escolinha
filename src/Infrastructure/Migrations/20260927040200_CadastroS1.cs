using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaEscolinha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CadastroS1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Aluno",
                columns: table => new
                {
                    Aluno_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NomeBusca = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DataNascimento = table.Column<DateOnly>(type: "date", nullable: false),
                    Cpf = table.Column<string>(type: "char(11)", unicode: false, fixedLength: true, maxLength: 11, nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    SituacaoDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    TrialAte = table.Column<DateOnly>(type: "date", nullable: true),
                    DataMatricula = table.Column<DateOnly>(type: "date", nullable: false),
                    DataDesligamento = table.Column<DateOnly>(type: "date", nullable: true),
                    MotivoDesligamento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ContatoEmergenciaNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ContatoEmergenciaTelefone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ContatoEmergenciaParentesco = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Alergias = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Restricoes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ObservacoesMedicas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Bairro = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Cep = table.Column<string>(type: "char(8)", unicode: false, fixedLength: true, maxLength: 8, nullable: true),
                    Cidade = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Complemento = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Logradouro = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Uf = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aluno", x => x.Aluno_ID);
                    table.ForeignKey(
                        name: "FK_Aluno_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Local",
                columns: table => new
                {
                    Local_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Capacidade = table.Column<int>(type: "int", nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Bairro = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Cep = table.Column<string>(type: "char(8)", unicode: false, fixedLength: true, maxLength: 8, nullable: true),
                    Cidade = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Complemento = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Logradouro = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Uf = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Local", x => x.Local_ID);
                    table.CheckConstraint("CK_Local_Capacidade", "[Capacidade] IS NULL OR [Capacidade] > 0");
                    table.ForeignKey(
                        name: "FK_Local_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Professor",
                columns: table => new
                {
                    Professor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NomeBusca = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Cpf = table.Column<string>(type: "char(11)", unicode: false, fixedLength: true, maxLength: 11, nullable: true),
                    Telefone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Pessoa_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Professor", x => x.Professor_ID);
                    table.ForeignKey(
                        name: "FK_Professor_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Professor_Pessoa",
                        column: x => x.Pessoa_ID,
                        principalTable: "Pessoa",
                        principalColumn: "Pessoa_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Responsavel",
                columns: table => new
                {
                    Responsavel_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NomeBusca = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Cpf = table.Column<string>(type: "char(11)", unicode: false, fixedLength: true, maxLength: 11, nullable: true),
                    Telefone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Pessoa_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Bairro = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Cep = table.Column<string>(type: "char(8)", unicode: false, fixedLength: true, maxLength: 8, nullable: true),
                    Cidade = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Complemento = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Logradouro = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Uf = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Responsavel", x => x.Responsavel_ID);
                    table.ForeignKey(
                        name: "FK_Responsavel_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Responsavel_Pessoa",
                        column: x => x.Pessoa_ID,
                        principalTable: "Pessoa",
                        principalColumn: "Pessoa_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FaixaDisponibilidade",
                columns: table => new
                {
                    FaixaDisponibilidade_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Professor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiaSemana = table.Column<int>(type: "int", nullable: false),
                    Inicio = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    Fim = table.Column<TimeOnly>(type: "time(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaixaDisponibilidade", x => x.FaixaDisponibilidade_ID);
                    table.CheckConstraint("CK_FaixaDisponibilidade_Horario", "[Fim] > [Inicio]");
                    table.ForeignKey(
                        name: "FK_FaixaDisponibilidade_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FaixaDisponibilidade_Professor",
                        column: x => x.Professor_ID,
                        principalTable: "Professor",
                        principalColumn: "Professor_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IndisponibilidadeProfessor",
                columns: table => new
                {
                    IndisponibilidadeProfessor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Professor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Inicio = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Fim = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RegistradaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RegistradaPorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndisponibilidadeProfessor", x => x.IndisponibilidadeProfessor_ID);
                    table.CheckConstraint("CK_IndisponibilidadeProfessor_Periodo", "[Fim] > [Inicio]");
                    table.ForeignKey(
                        name: "FK_IndisponibilidadeProfessor_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IndisponibilidadeProfessor_Professor",
                        column: x => x.Professor_ID,
                        principalTable: "Professor",
                        principalColumn: "Professor_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Turma",
                columns: table => new
                {
                    Turma_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Modalidade_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Local_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Professor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Capacidade = table.Column<int>(type: "int", nullable: false),
                    Mensalidade = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MinimoConfirmados = table.Column<int>(type: "int", nullable: false),
                    MetaFrequenciaPercentual = table.Column<int>(type: "int", nullable: true),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    EncerradaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    MotivoEncerramento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turma", x => x.Turma_ID);
                    table.CheckConstraint("CK_Turma_Capacidade", "[Capacidade] > 0");
                    table.CheckConstraint("CK_Turma_Mensalidade", "[Mensalidade] >= 0");
                    table.CheckConstraint("CK_Turma_MetaFrequencia", "[MetaFrequenciaPercentual] IS NULL OR [MetaFrequenciaPercentual] BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_Turma_MinimoConfirmados", "[MinimoConfirmados] > 0");
                    table.ForeignKey(
                        name: "FK_Turma_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Turma_Local",
                        column: x => x.Local_ID,
                        principalTable: "Local",
                        principalColumn: "Local_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Turma_Modalidade",
                        column: x => x.Modalidade_ID,
                        principalTable: "Modalidade",
                        principalColumn: "Modalidade_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Turma_Professor",
                        column: x => x.Professor_ID,
                        principalTable: "Professor",
                        principalColumn: "Professor_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsentimentoAluno",
                columns: table => new
                {
                    ConsentimentoAluno_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Aluno_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Finalidade = table.Column<int>(type: "int", nullable: false),
                    Concedido = table.Column<bool>(type: "bit", nullable: false),
                    VersaoTermo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Responsavel_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    RegistradoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RegistradoPor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RegistradoPorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RevogadoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RevogadoPor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevogadoPorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    MotivoRevogacao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentimentoAluno", x => x.ConsentimentoAluno_ID);
                    table.ForeignKey(
                        name: "FK_ConsentimentoAluno_Aluno",
                        column: x => x.Aluno_ID,
                        principalTable: "Aluno",
                        principalColumn: "Aluno_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsentimentoAluno_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsentimentoAluno_Responsavel",
                        column: x => x.Responsavel_ID,
                        principalTable: "Responsavel",
                        principalColumn: "Responsavel_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VinculoAlunoResponsavel",
                columns: table => new
                {
                    VinculoAlunoResponsavel_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Aluno_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Responsavel_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Parentesco = table.Column<int>(type: "int", nullable: false),
                    Pagador = table.Column<bool>(type: "bit", nullable: false),
                    RecebeCobranca = table.Column<bool>(type: "bit", nullable: false),
                    RecebeNotificacao = table.Column<bool>(type: "bit", nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VinculoAlunoResponsavel", x => x.VinculoAlunoResponsavel_ID);
                    table.CheckConstraint("CK_VinculoAlunoResponsavel_Pagador", "[Pagador] = 0 OR [RecebeCobranca] = 1");
                    table.ForeignKey(
                        name: "FK_VinculoAlunoResponsavel_Aluno",
                        column: x => x.Aluno_ID,
                        principalTable: "Aluno",
                        principalColumn: "Aluno_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VinculoAlunoResponsavel_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VinculoAlunoResponsavel_Responsavel",
                        column: x => x.Responsavel_ID,
                        principalTable: "Responsavel",
                        principalColumn: "Responsavel_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HorarioTurma",
                columns: table => new
                {
                    HorarioTurma_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Turma_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiaSemana = table.Column<int>(type: "int", nullable: false),
                    Inicio = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    Fim = table.Column<TimeOnly>(type: "time(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HorarioTurma", x => x.HorarioTurma_ID);
                    table.CheckConstraint("CK_HorarioTurma_Horario", "[Fim] > [Inicio]");
                    table.ForeignKey(
                        name: "FK_HorarioTurma_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HorarioTurma_Turma",
                        column: x => x.Turma_ID,
                        principalTable: "Turma",
                        principalColumn: "Turma_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Matricula",
                columns: table => new
                {
                    Matricula_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Aluno_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Turma_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    DataFim = table.Column<DateOnly>(type: "date", nullable: true),
                    Mensalidade = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    MotivoEncerramento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matricula", x => x.Matricula_ID);
                    table.CheckConstraint("CK_Matricula_Mensalidade", "[Mensalidade] >= 0");
                    table.CheckConstraint("CK_Matricula_Periodo", "[DataFim] IS NULL OR [DataFim] >= [DataInicio]");
                    table.ForeignKey(
                        name: "FK_Matricula_Aluno",
                        column: x => x.Aluno_ID,
                        principalTable: "Aluno",
                        principalColumn: "Aluno_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matricula_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matricula_Turma",
                        column: x => x.Turma_ID,
                        principalTable: "Turma",
                        principalColumn: "Turma_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessaoTreino",
                columns: table => new
                {
                    SessaoTreino_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Turma_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HorarioTurma_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Inicio = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Fim = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Local_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Professor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CanceladaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CanceladaPor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CanceladaPorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessaoTreino", x => x.SessaoTreino_ID);
                    table.CheckConstraint("CK_SessaoTreino_Horario", "[Fim] > [Inicio]");
                    table.ForeignKey(
                        name: "FK_SessaoTreino_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessaoTreino_HorarioTurma",
                        column: x => x.HorarioTurma_ID,
                        principalTable: "HorarioTurma",
                        principalColumn: "HorarioTurma_ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SessaoTreino_Local",
                        column: x => x.Local_ID,
                        principalTable: "Local",
                        principalColumn: "Local_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessaoTreino_Professor",
                        column: x => x.Professor_ID,
                        principalTable: "Professor",
                        principalColumn: "Professor_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessaoTreino_Turma",
                        column: x => x.Turma_ID,
                        principalTable: "Turma",
                        principalColumn: "Turma_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aluno_Escolinha_NomeBusca",
                table: "Aluno",
                columns: new[] { "Escolinha_ID", "NomeBusca" });

            migrationBuilder.CreateIndex(
                name: "IX_Aluno_Escolinha_Situacao",
                table: "Aluno",
                columns: new[] { "Escolinha_ID", "Situacao" });

            migrationBuilder.CreateIndex(
                name: "UQ_Aluno_Escolinha_Cpf",
                table: "Aluno",
                columns: new[] { "Escolinha_ID", "Cpf" },
                unique: true,
                filter: "[Cpf] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentimentoAluno_Aluno_ID",
                table: "ConsentimentoAluno",
                column: "Aluno_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentimentoAluno_Escolinha_Aluno_Finalidade_Em",
                table: "ConsentimentoAluno",
                columns: new[] { "Escolinha_ID", "Aluno_ID", "Finalidade", "RegistradoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentimentoAluno_Responsavel_ID",
                table: "ConsentimentoAluno",
                column: "Responsavel_ID");

            migrationBuilder.CreateIndex(
                name: "IX_FaixaDisponibilidade_Escolinha_Professor",
                table: "FaixaDisponibilidade",
                columns: new[] { "Escolinha_ID", "Professor_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_FaixaDisponibilidade_Professor_ID",
                table: "FaixaDisponibilidade",
                column: "Professor_ID");

            migrationBuilder.CreateIndex(
                name: "IX_HorarioTurma_Escolinha_Turma",
                table: "HorarioTurma",
                columns: new[] { "Escolinha_ID", "Turma_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_HorarioTurma_Turma_ID",
                table: "HorarioTurma",
                column: "Turma_ID");

            migrationBuilder.CreateIndex(
                name: "IX_IndisponibilidadeProfessor_Escolinha_Professor_Inicio",
                table: "IndisponibilidadeProfessor",
                columns: new[] { "Escolinha_ID", "Professor_ID", "Inicio" });

            migrationBuilder.CreateIndex(
                name: "IX_IndisponibilidadeProfessor_Professor_ID",
                table: "IndisponibilidadeProfessor",
                column: "Professor_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_Local_Escolinha_Nome",
                table: "Local",
                columns: new[] { "Escolinha_ID", "Nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matricula_Aluno_ID",
                table: "Matricula",
                column: "Aluno_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Matricula_Escolinha_Turma_Situacao",
                table: "Matricula",
                columns: new[] { "Escolinha_ID", "Turma_ID", "Situacao" });

            migrationBuilder.CreateIndex(
                name: "IX_Matricula_Turma_ID",
                table: "Matricula",
                column: "Turma_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_Matricula_Escolinha_Aluno_Turma_Ativa",
                table: "Matricula",
                columns: new[] { "Escolinha_ID", "Aluno_ID", "Turma_ID" },
                unique: true,
                filter: "[Situacao] = 10");

            migrationBuilder.CreateIndex(
                name: "IX_Professor_Escolinha_NomeBusca",
                table: "Professor",
                columns: new[] { "Escolinha_ID", "NomeBusca" });

            migrationBuilder.CreateIndex(
                name: "IX_Professor_Pessoa_ID",
                table: "Professor",
                column: "Pessoa_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_Professor_Escolinha_Cpf",
                table: "Professor",
                columns: new[] { "Escolinha_ID", "Cpf" },
                unique: true,
                filter: "[Cpf] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_Professor_Escolinha_Pessoa",
                table: "Professor",
                columns: new[] { "Escolinha_ID", "Pessoa_ID" },
                unique: true,
                filter: "[Pessoa_ID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Responsavel_Escolinha_NomeBusca",
                table: "Responsavel",
                columns: new[] { "Escolinha_ID", "NomeBusca" });

            migrationBuilder.CreateIndex(
                name: "IX_Responsavel_Escolinha_Telefone",
                table: "Responsavel",
                columns: new[] { "Escolinha_ID", "Telefone" });

            migrationBuilder.CreateIndex(
                name: "IX_Responsavel_Pessoa_ID",
                table: "Responsavel",
                column: "Pessoa_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_Responsavel_Escolinha_Cpf",
                table: "Responsavel",
                columns: new[] { "Escolinha_ID", "Cpf" },
                unique: true,
                filter: "[Cpf] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_Responsavel_Escolinha_Pessoa",
                table: "Responsavel",
                columns: new[] { "Escolinha_ID", "Pessoa_ID" },
                unique: true,
                filter: "[Pessoa_ID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SessaoTreino_Escolinha_Local_Inicio",
                table: "SessaoTreino",
                columns: new[] { "Escolinha_ID", "Local_ID", "Inicio" });

            migrationBuilder.CreateIndex(
                name: "IX_SessaoTreino_Escolinha_Professor_Inicio",
                table: "SessaoTreino",
                columns: new[] { "Escolinha_ID", "Professor_ID", "Inicio" });

            migrationBuilder.CreateIndex(
                name: "IX_SessaoTreino_Escolinha_Turma_Data",
                table: "SessaoTreino",
                columns: new[] { "Escolinha_ID", "Turma_ID", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_SessaoTreino_HorarioTurma_ID",
                table: "SessaoTreino",
                column: "HorarioTurma_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SessaoTreino_Local_ID",
                table: "SessaoTreino",
                column: "Local_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SessaoTreino_Professor_ID",
                table: "SessaoTreino",
                column: "Professor_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SessaoTreino_Turma_ID",
                table: "SessaoTreino",
                column: "Turma_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_SessaoTreino_Escolinha_Horario_Data",
                table: "SessaoTreino",
                columns: new[] { "Escolinha_ID", "HorarioTurma_ID", "Data" },
                unique: true,
                filter: "[HorarioTurma_ID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Turma_Escolinha_Professor",
                table: "Turma",
                columns: new[] { "Escolinha_ID", "Professor_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_Turma_Local_ID",
                table: "Turma",
                column: "Local_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Turma_Modalidade_ID",
                table: "Turma",
                column: "Modalidade_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Turma_Professor_ID",
                table: "Turma",
                column: "Professor_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_Turma_Escolinha_Nome_Ativa",
                table: "Turma",
                columns: new[] { "Escolinha_ID", "Nome" },
                unique: true,
                filter: "[Situacao] = 10");

            migrationBuilder.CreateIndex(
                name: "IX_VinculoAlunoResponsavel_Aluno_ID",
                table: "VinculoAlunoResponsavel",
                column: "Aluno_ID");

            migrationBuilder.CreateIndex(
                name: "IX_VinculoAlunoResponsavel_Escolinha_Responsavel",
                table: "VinculoAlunoResponsavel",
                columns: new[] { "Escolinha_ID", "Responsavel_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_VinculoAlunoResponsavel_Responsavel_ID",
                table: "VinculoAlunoResponsavel",
                column: "Responsavel_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_VinculoAlunoResponsavel_Escolinha_Aluno_Responsavel",
                table: "VinculoAlunoResponsavel",
                columns: new[] { "Escolinha_ID", "Aluno_ID", "Responsavel_ID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentimentoAluno");

            migrationBuilder.DropTable(
                name: "FaixaDisponibilidade");

            migrationBuilder.DropTable(
                name: "IndisponibilidadeProfessor");

            migrationBuilder.DropTable(
                name: "Matricula");

            migrationBuilder.DropTable(
                name: "SessaoTreino");

            migrationBuilder.DropTable(
                name: "VinculoAlunoResponsavel");

            migrationBuilder.DropTable(
                name: "HorarioTurma");

            migrationBuilder.DropTable(
                name: "Aluno");

            migrationBuilder.DropTable(
                name: "Responsavel");

            migrationBuilder.DropTable(
                name: "Turma");

            migrationBuilder.DropTable(
                name: "Local");

            migrationBuilder.DropTable(
                name: "Professor");
        }
    }
}
