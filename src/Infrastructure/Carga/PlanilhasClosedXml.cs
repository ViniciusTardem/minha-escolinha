using System.Globalization;
using ClosedXML.Excel;
using MinhaEscolinha.Application.Carga;

namespace MinhaEscolinha.Infrastructure.Carga;

/// <summary>
/// A planilha da carga inicial com o ClosedXML, o mesmo dos relatórios do DP: ler a primeira aba (BL-M12-01) e gerar o
/// modelo para baixar. As células viram texto aqui; a interpretação (data, CPF, sim/não) é da conferência.
/// </summary>
public sealed class PlanilhasClosedXml : IPlanilhas
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public ResultadoLeituraPlanilha Ler(Stream arquivo)
    {
        XLWorkbook livro;
        try
        {
            livro = new XLWorkbook(arquivo);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new ResultadoLeituraPlanilha(null,
                "Não deu para abrir o arquivo como planilha .xlsx. Salve de novo no Excel ou no Google Planilhas e envie outra vez.");
        }

        using (livro)
        {
            var aba = livro.Worksheets.FirstOrDefault();
            if (aba?.RangeUsed() is not { } usado) return new ResultadoLeituraPlanilha(null, "A primeira aba da planilha está vazia.");

            var primeiraLinha = usado.FirstRow().RowNumber();
            var ultimaLinha = usado.LastRow().RowNumber();
            var primeiraColuna = usado.FirstColumn().ColumnNumber();
            var ultimaColuna = usado.LastColumn().ColumnNumber();
            var colunas = Enumerable.Range(primeiraColuna, ultimaColuna - primeiraColuna + 1).ToList();

            // O cabeçalho é a primeira linha com dado; as outras linhas vazias no meio são puladas.
            var cabecalhos = colunas.Select(c => Texto(aba.Cell(primeiraLinha, c)) ?? string.Empty).ToList();
            var linhas = new List<LinhaLida>();
            for (var r = primeiraLinha + 1; r <= ultimaLinha; r++)
            {
                var celulas = colunas.Select(c => Texto(aba.Cell(r, c))).ToList();
                if (celulas.All(string.IsNullOrWhiteSpace)) continue;
                linhas.Add(new LinhaLida(r, celulas));
            }
            return new ResultadoLeituraPlanilha(new PlanilhaLida(cabecalhos, linhas), null);
        }
    }

    /// <summary>
    /// O valor da célula em texto. Número inteiro sai só com os dígitos (CPF e celular digitados como número); número com
    /// casas decimais sai com vírgula, para a mensalidade não virar milhar; data sai em dd/MM/aaaa.
    /// </summary>
    private static string? Texto(IXLCell celula)
    {
        var valor = celula.Value;
        return valor.Type switch
        {
            XLDataType.Blank => null,
            XLDataType.Text => valor.GetText(),
            XLDataType.Number => valor.GetNumber() is var n && Math.Abs(n - Math.Round(n)) < 1e-9
                ? Math.Round(n).ToString("0", CultureInfo.InvariantCulture)
                : valor.GetNumber().ToString("0.##########", PtBr),
            XLDataType.DateTime => valor.GetDateTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            XLDataType.Boolean => valor.GetBoolean() ? "sim" : "não",
            _ => celula.GetFormattedString()
        };
    }

    public byte[] Modelo(IReadOnlyList<string> turmas)
    {
        using var livro = new XLWorkbook();
        var alunos = livro.AddWorksheet("Alunos");
        var campos = CatalogoCamposImportacao.Todos;
        for (var i = 0; i < campos.Count; i++)
        {
            var coluna = i + 1;
            alunos.Cell(1, coluna).Value = campos[i].Rotulo;
            alunos.Column(coluna).Width = Math.Max(16, campos[i].Rotulo.Length + 4);
            // CPF e celular como texto: o Excel não come os zeros à esquerda nem vira notação científica.
            if (campos[i].Campo is CampoImportacao.AlunoCpf or CampoImportacao.Responsavel1Cpf or CampoImportacao.Responsavel2Cpf
                or CampoImportacao.Responsavel1Celular or CampoImportacao.Responsavel2Celular or CampoImportacao.EmergenciaTelefone)
                alunos.Column(coluna).Style.NumberFormat.Format = "@";
            if (campos[i].Obrigatorio) alunos.Cell(1, coluna).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#DBEAFE"));
        }
        alunos.Row(1).Style.Font.SetBold();
        alunos.SheetView.FreezeRows(1);

        var instrucoes = livro.AddWorksheet("Instruções");
        instrucoes.Cell(1, 1).Value = "Carga inicial da Minha Escolinha";
        instrucoes.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        instrucoes.Cell(2, 1).Value =
            "Uma linha por aluno, na aba Alunos. As colunas em azul são obrigatórias. Irmãos repetem os dados do responsável: vira um cadastro só.";
        var linha = 4;
        foreach (var (texto, coluna) in new[] { ("Coluna", 1), ("Obrigatória", 2), ("Exemplo", 3), ("Como preencher", 4) })
            instrucoes.Cell(linha, coluna).Value = texto;
        instrucoes.Row(linha).Style.Font.SetBold();
        foreach (var campo in campos)
        {
            linha++;
            instrucoes.Cell(linha, 1).Value = campo.Rotulo;
            instrucoes.Cell(linha, 2).Value = campo.Obrigatorio ? "sim" : "não";
            instrucoes.Cell(linha, 3).Value = campo.Exemplo;
            instrucoes.Cell(linha, 4).Value = CatalogoCamposImportacao.Orientacao(campo.Campo);
        }
        linha += 2;
        instrucoes.Cell(linha, 1).Value = turmas.Count == 0 ? "Nenhuma turma ativa: cadastre as turmas antes de importar." : "Turmas ativas";
        instrucoes.Cell(linha, 1).Style.Font.SetBold();
        foreach (var turma in turmas) instrucoes.Cell(++linha, 1).Value = turma;
        instrucoes.Column(1).Width = 38;
        instrucoes.Column(2).Width = 12;
        instrucoes.Column(3).Width = 20;
        instrucoes.Column(4).Width = 90;

        using var memoria = new MemoryStream();
        livro.SaveAs(memoria);
        return memoria.ToArray();
    }
}
