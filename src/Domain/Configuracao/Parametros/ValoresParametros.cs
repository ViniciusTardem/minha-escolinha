using System.Globalization;

namespace MinhaEscolinha.Domain.Configuracao.Parametros;

/// <summary>
/// Os valores efetivos dos parâmetros de uma escolinha num momento: o gravado ou, se faltar, o padrão do catálogo.
/// Lido uma vez por escopo (requisição ou job) e consultado pelos módulos com os acessos tipados abaixo.
/// </summary>
public sealed class ValoresParametros
{
    private readonly IReadOnlyDictionary<string, string?> _gravados;

    public ValoresParametros(IReadOnlyDictionary<string, string?> gravados) => _gravados = gravados;

    public static ValoresParametros SoPadroes() => new(new Dictionary<string, string?>());

    /// <summary>O texto efetivo (gravado ou padrão) — <c>null</c> quando o parâmetro não tem padrão nem valor.</summary>
    public string? Texto(string chave)
    {
        var definicao = CatalogoParametros.ObterObrigatorio(chave);
        return _gravados.TryGetValue(chave, out var valor) && valor is not null ? valor : definicao.ValorPadrao;
    }

    public int Inteiro(string chave) =>
        int.Parse(TextoObrigatorio(chave), NumberStyles.Integer, CultureInfo.InvariantCulture);

    public decimal Decimal(string chave) =>
        decimal.Parse(TextoObrigatorio(chave), NumberStyles.Number, CultureInfo.InvariantCulture);

    public bool Booleano(string chave) => string.Equals(TextoObrigatorio(chave), "true", StringComparison.OrdinalIgnoreCase);

    public JanelaHorario Janela(string chave) =>
        JanelaHorario.TentarLer(TextoObrigatorio(chave), out var janela)
            ? janela
            : throw new FormatException($"Parâmetro {chave} com janela inválida.");

    public IReadOnlyList<int> ListaDias(string chave) =>
        TextoObrigatorio(chave).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => int.Parse(d, CultureInfo.InvariantCulture)).ToList();

    public string Escolha(string chave) => TextoObrigatorio(chave);

    public ParametrosPresenca Presenca => new(
        Escolha(ChavesParametro.PresencaRegraFaltas) == "Consecutivas" ? RegraFaltas.Consecutivas : RegraFaltas.NoMes,
        Inteiro(ChavesParametro.PresencaLimiteFaltas),
        Escolha(ChavesParametro.PresencaTomAlerta),
        Inteiro(ChavesParametro.PresencaJanelaAntiDuplicidade),
        Decimal(ChavesParametro.PresencaRiscoPercentual),
        Inteiro(ChavesParametro.PresencaRiscoJanela),
        Booleano(ChavesParametro.PresencaFaltaJustificadaConta),
        Inteiro(ChavesParametro.PresencaRiscoMinimoSessoes),
        Booleano(ChavesParametro.PresencaRiscoUsarMetaTurma));

    public ParametrosFinanceiro Financeiro => new(
        Decimal(ChavesParametro.FinanceiroMulta),
        Decimal(ChavesParametro.FinanceiroJuros),
        Inteiro(ChavesParametro.FinanceiroDiaVencimento),
        Inteiro(ChavesParametro.FinanceiroEmissaoDiasAntes),
        ListaDias(ChavesParametro.FinanceiroReguaDiasAntes),
        Booleano(ChavesParametro.FinanceiroReguaNoDia),
        ListaDias(ChavesParametro.FinanceiroReguaDiasDepois),
        Booleano(ChavesParametro.FinanceiroPortalTodosResponsaveis));

    public ParametrosConvocacao Convocacao => new(
        Escolha(ChavesParametro.ConvocacaoModoDisparo) == "Janela",
        Janela(ChavesParametro.ConvocacaoJanelaDisparo),
        Inteiro(ChavesParametro.ConvocacaoLembreteHorasAntes),
        Booleano(ChavesParametro.ConvocacaoSinalizarInadimplencia),
        Inteiro(ChavesParametro.ConvocacaoDiasAtrasoSinalizacao),
        Booleano(ChavesParametro.ConvocacaoBloquearInadimplente),
        Booleano(ChavesParametro.ConvocacaoNovaConfirmacaoAoMudar),
        Booleano(ChavesParametro.ConvocacaoProfessorSoPropriaTurma),
        Inteiro(ChavesParametro.ConvocacaoAvisoMudancaHorasAntes));

    public ParametrosMensagem Mensagem => new(
        Janela(ChavesParametro.MensagemJanelaSilencio),
        Inteiro(ChavesParametro.MensagemAvisoSaldoCreditos),
        Escolha(ChavesParametro.MensagemTom),
        Booleano(ChavesParametro.MensagemDescadastroPorPalavra));

    public ParametrosMatricula Matricula => new(
        Inteiro(ChavesParametro.MatriculaTrialDiasAviso),
        Texto(ChavesParametro.TurmaMensalidade) is { } mensalidade
            ? decimal.Parse(mensalidade, NumberStyles.Number, CultureInfo.InvariantCulture)
            : null,
        Inteiro(ChavesParametro.TurmaCapacidade),
        Inteiro(ChavesParametro.TurmaMinimoConfirmados));

    private string TextoObrigatorio(string chave) =>
        Texto(chave) ?? throw new InvalidOperationException($"Parâmetro {chave} sem valor e sem padrão.");
}

public enum RegraFaltas
{
    NoMes = 10,
    Consecutivas = 20
}

public sealed record ParametrosPresenca(
    RegraFaltas Regra,
    int LimiteFaltas,
    string Tom,
    int JanelaAntiDuplicidadeDias,
    decimal FrequenciaMinimaPercentual,
    int JanelaRiscoDias,
    bool FaltaJustificadaContaParaAlerta,
    int MinimoSessoesRisco = 4,
    bool UsarMetaDaTurma = true);

public sealed record ParametrosFinanceiro(
    decimal MultaPercentual,
    decimal JurosPercentualMes,
    int DiaVencimento,
    int EmissaoDiasAntesVencimento,
    IReadOnlyList<int> LembretesDiasAntes,
    bool LembreteNoDia,
    IReadOnlyList<int> CobrancasDiasDepois,
    bool TodosResponsaveisVeemNoPortal = false);

public sealed record ParametrosConvocacao(
    bool SoDentroDaJanela,
    JanelaHorario JanelaDisparo,
    int LembreteHorasAntes,
    bool SinalizarInadimplencia,
    int DiasAtrasoSinalizacao,
    bool BloquearInadimplente,
    bool NovaConfirmacaoAoMudarHorario = true,
    bool ProfessorSoPropriaTurma = true,
    int AvisoMudancaRespostaHorasAntes = 24);

public sealed record ParametrosMensagem(
    JanelaHorario JanelaSilencio,
    int AvisoSaldoCreditosAbaixoDe,
    string Tom = "Cordial",
    bool DescadastroPorPalavra = true);

/// <summary>Matrícula e os valores iniciais das turmas novas (a turma guarda o próprio valor, S1).</summary>
public sealed record ParametrosMatricula(
    int TrialDiasAvisoAntes,
    decimal? MensalidadeInicial,
    int CapacidadeInicial,
    int MinimoConfirmadosInicial);
