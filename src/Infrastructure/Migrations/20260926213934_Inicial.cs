using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaEscolinha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FriendlyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Xml = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Escolinha",
                columns: table => new
                {
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RazaoSocial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Cnpj = table.Column<string>(type: "char(14)", unicode: false, fixedLength: true, maxLength: 14, nullable: true),
                    Telefone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
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
                    table.PrimaryKey("PK_Escolinha", x => x.Escolinha_ID);
                });

            migrationBuilder.CreateTable(
                name: "Pessoa",
                columns: table => new
                {
                    Pessoa_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Cpf = table.Column<string>(type: "char(11)", unicode: false, fixedLength: true, maxLength: 11, nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pessoa", x => x.Pessoa_ID);
                });

            migrationBuilder.CreateTable(
                name: "ContaAsaasEscolinha",
                columns: table => new
                {
                    ContaAsaasEscolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ambiente = table.Column<int>(type: "int", nullable: false),
                    ChaveApiProtegida = table.Column<string>(type: "varchar(2000)", unicode: false, maxLength: 2000, nullable: false),
                    FinalChave = table.Column<string>(type: "varchar(4)", unicode: false, maxLength: 4, nullable: false),
                    VinculadaEm = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    VinculadaPor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContaAsaasEscolinha", x => x.ContaAsaasEscolinha_ID);
                    table.ForeignKey(
                        name: "FK_ContaAsaasEscolinha_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContaCreditos",
                columns: table => new
                {
                    ContaCreditos_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Plano = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Pacote = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Saldo = table.Column<int>(type: "int", nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContaCreditos", x => x.ContaCreditos_ID);
                    table.CheckConstraint("CK_ContaCreditos_Saldo", "[Saldo] >= 0");
                    table.ForeignKey(
                        name: "FK_ContaCreditos_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Modalidade",
                columns: table => new
                {
                    Modalidade_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Perfil = table.Column<int>(type: "int", nullable: false),
                    TemAdversario = table.Column<bool>(type: "bit", nullable: false),
                    TemPlacarSumula = table.Column<bool>(type: "bit", nullable: false),
                    TemClassificacao = table.Column<bool>(type: "bit", nullable: false),
                    TemConvocacao = table.Column<bool>(type: "bit", nullable: false),
                    TemGraduacao = table.Column<bool>(type: "bit", nullable: false),
                    TemApresentacao = table.Column<bool>(type: "bit", nullable: false),
                    DuracaoPadraoEventoMinutos = table.Column<int>(type: "int", nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modalidade", x => x.Modalidade_ID);
                    table.ForeignKey(
                        name: "FK_Modalidade_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimentoCredito",
                columns: table => new
                {
                    MovimentoCredito_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    SaldoApos = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Em = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Autor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AutorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentoCredito", x => x.MovimentoCredito_ID);
                    table.ForeignKey(
                        name: "FK_MovimentoCredito_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParametroEscolinha",
                columns: table => new
                {
                    ParametroEscolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Chave = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametroEscolinha", x => x.ParametroEscolinha_ID);
                    table.ForeignKey(
                        name: "FK_ParametroEscolinha_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistroAlteracao",
                columns: table => new
                {
                    RegistroAlteracao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Entidade = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    Registro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Campo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ValorAnterior = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ValorNovo = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Autor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AutorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Em = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Ip = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistroAlteracao", x => x.RegistroAlteracao_ID);
                    table.ForeignKey(
                        name: "FK_RegistroAlteracao_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Credencial",
                columns: table => new
                {
                    Pessoa_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Login = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SenhaHash = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Ativa = table.Column<bool>(type: "bit", nullable: false),
                    AdministradorPlataforma = table.Column<bool>(type: "bit", nullable: false),
                    TrocarSenha = table.Column<bool>(type: "bit", nullable: false),
                    TentativasFalhas = table.Column<int>(type: "int", nullable: false),
                    BloqueadoAte = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UltimoAcesso = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CarimboSeguranca = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Credencial", x => x.Pessoa_ID);
                    table.ForeignKey(
                        name: "FK_Credencial_Pessoa",
                        column: x => x.Pessoa_ID,
                        principalTable: "Pessoa",
                        principalColumn: "Pessoa_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VinculoAtuacao",
                columns: table => new
                {
                    VinculoAtuacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Escolinha_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Pessoa_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Papel = table.Column<int>(type: "int", nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    RevogadoEm = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RevogadoPor_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MotivoRevogacao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioCadastro_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UsuarioAtualizacao_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VinculoAtuacao", x => x.VinculoAtuacao_ID);
                    table.ForeignKey(
                        name: "FK_VinculoAtuacao_Escolinha",
                        column: x => x.Escolinha_ID,
                        principalTable: "Escolinha",
                        principalColumn: "Escolinha_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VinculoAtuacao_Pessoa",
                        column: x => x.Pessoa_ID,
                        principalTable: "Pessoa",
                        principalColumn: "Pessoa_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_ContaAsaasEscolinha_Escolinha",
                table: "ContaAsaasEscolinha",
                column: "Escolinha_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ContaCreditos_Escolinha",
                table: "ContaCreditos",
                column: "Escolinha_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Credencial_Login",
                table: "Credencial",
                column: "Login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Modalidade_Escolinha_Nome",
                table: "Modalidade",
                columns: new[] { "Escolinha_ID", "Nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentoCredito_Escolinha_Em",
                table: "MovimentoCredito",
                columns: new[] { "Escolinha_ID", "Em" });

            migrationBuilder.CreateIndex(
                name: "UQ_ParametroEscolinha_Escolinha_Chave",
                table: "ParametroEscolinha",
                columns: new[] { "Escolinha_ID", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Pessoa_Cpf",
                table: "Pessoa",
                column: "Cpf",
                unique: true,
                filter: "[Cpf] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RegistroAlteracao_Escolinha_Entidade_Em",
                table: "RegistroAlteracao",
                columns: new[] { "Escolinha_ID", "Entidade", "Em" });

            migrationBuilder.CreateIndex(
                name: "IX_VinculoAtuacao_Pessoa_Situacao",
                table: "VinculoAtuacao",
                columns: new[] { "Pessoa_ID", "Situacao" });

            migrationBuilder.CreateIndex(
                name: "UQ_VinculoAtuacao_Escolinha_Pessoa_Papel",
                table: "VinculoAtuacao",
                columns: new[] { "Escolinha_ID", "Pessoa_ID", "Papel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContaAsaasEscolinha");

            migrationBuilder.DropTable(
                name: "ContaCreditos");

            migrationBuilder.DropTable(
                name: "Credencial");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "Modalidade");

            migrationBuilder.DropTable(
                name: "MovimentoCredito");

            migrationBuilder.DropTable(
                name: "ParametroEscolinha");

            migrationBuilder.DropTable(
                name: "RegistroAlteracao");

            migrationBuilder.DropTable(
                name: "VinculoAtuacao");

            migrationBuilder.DropTable(
                name: "Escolinha");

            migrationBuilder.DropTable(
                name: "Pessoa");
        }
    }
}
