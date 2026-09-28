using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaEscolinha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TemplateNomeAnterior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NomeAnterior",
                table: "TemplateMensagem",
                type: "varchar(120)",
                unicode: false,
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NomeAnterior",
                table: "TemplateMensagem");
        }
    }
}
