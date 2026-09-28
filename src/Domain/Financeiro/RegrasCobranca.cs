using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Domain.Financeiro;

/// <summary>Uma matrícula como a projeção das mensalidades a enxerga.</summary>
/// <param name="AlunoAtivo">Trial não é cobrado (P20); inativo é pausa, sem cobrança (spec §6); desistente saiu.</param>
public sealed record MatriculaParaCobranca(
    Guid MatriculaId,
    Guid AlunoId,
    Guid TurmaId,
    DateOnly Inicio,
    DateOnly? Fim,
    decimal Mensalidade,
    bool AlunoAtivo,
    int PeriodicidadeMeses = 1);

/// <summary>O que a projeção vai fazer: criar as previstas que faltam, ajustar as que mudaram e retirar as que sobraram.</summary>
public sealed record PlanoCobrancas(IReadOnlyList<Cobranca> Criar, IReadOnlyList<Cobranca> Ajustadas, IReadOnlyList<Cobranca> Remover)
{
    public bool Vazio => Criar.Count == 0 && Ajustadas.Count == 0 && Remover.Count == 0;
}

/// <summary>
/// A recorrência da matrícula (BL-M06-01): a matrícula traz o valor e o período; a mensalidade de cada mês é projetada
/// como cobrança prevista, num horizonte rolante até o fim do mês seguinte, como as sessões de treino (D31).
/// <list type="bullet">
/// <item>idempotente pela chave (matrícula, competência): rodar de novo não duplica;</item>
/// <item>turma trimestral, semestral ou anual (P44): a cobrança sai no mês de início da matrícula e a cada período, com o
/// valor do período; nos meses do meio, nada;</item>
/// <item>só competências que ainda não venceram: o sistema nunca cobra para trás sozinho;</item>
/// <item>só aluno ativo, com mensalidade maior que zero e matrícula vigente no vencimento;</item>
/// <item>a primeira da matrícula espera a confirmação da gestão (P17: entrada no meio do mês, pró-rata);</item>
/// <item>a prevista automática acompanha a matrícula (valor e vencimento) e sai quando deixa de valer. A confirmada, a
/// cancelada e a emitida nunca são tocadas: são decisão de alguém.</item>
/// </list>
/// </summary>
public static class PlanejadorCobrancas
{
    public static DateOnly CompetenciaDe(DateOnly data) => new(data.Year, data.Month, 1);

    public static DateOnly VencimentoDe(DateOnly competencia, int dia) =>
        new(competencia.Year, competencia.Month, Math.Clamp(dia, 1, DateTime.DaysInMonth(competencia.Year, competencia.Month)));

    /// <summary>Último dia do mês seguinte ao de hoje.</summary>
    public static DateOnly FimDoHorizonte(DateOnly hoje) => new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(2).AddDays(-1);

    public static int MesesEntre(DateOnly de, DateOnly ate) => (ate.Year - de.Year) * 12 + ate.Month - de.Month;

    /// <param name="matriculas">As matrículas ativas e as encerradas há pouco (as previstas delas podem ter de sair).</param>
    /// <param name="existentes">As cobranças dessas matrículas nas competências do horizonte, em qualquer situação.</param>
    /// <param name="comHistorico">As matrículas que já tiveram alguma cobrança, em qualquer época: a primeira espera confirmação.</param>
    public static PlanoCobrancas Planejar(IReadOnlyCollection<MatriculaParaCobranca> matriculas, IReadOnlyCollection<Cobranca> existentes,
        IEnumerable<Guid> comHistorico, DateOnly hoje, int diaVencimento)
    {
        var fim = FimDoHorizonte(hoje);
        // A via mais recente do mês é a que vale (P43: a cobrança de novo depois de um estorno).
        var porChave = existentes.GroupBy(c => (c.Matricula_ID, c.Competencia)).ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Via).First());
        var comCobranca = existentes.Select(c => c.Matricula_ID).Concat(comHistorico).ToHashSet();
        var criar = new List<Cobranca>();
        var ajustadas = new List<Cobranca>();
        var remover = new List<Cobranca>();
        var olhadas = new HashSet<Cobranca>();

        foreach (var m in matriculas)
        {
            for (var competencia = CompetenciaDe(m.Inicio); ; competencia = competencia.AddMonths(1))
            {
                var vencimento = VencimentoDe(competencia, diaVencimento);
                if (vencimento > fim) break;
                if (vencimento < hoje) continue;

                var noPeriodo = MesesEntre(CompetenciaDe(m.Inicio), competencia) % Math.Max(1, m.PeriodicidadeMeses) == 0;
                var vale = noPeriodo && m.AlunoAtivo && m.Mensalidade > 0 && m.Inicio <= vencimento && (m.Fim is null || vencimento <= m.Fim);
                if (porChave.TryGetValue((m.MatriculaId, competencia), out var existente))
                {
                    olhadas.Add(existente);
                    if (existente.Situacao != SituacaoCobranca.Prevista || existente.Confirmada) continue;
                    if (!vale) remover.Add(existente);
                    else if (existente.AjustarProjecao(m.Mensalidade, vencimento)) ajustadas.Add(existente);
                    continue;
                }

                if (!vale) continue;
                criar.Add(Cobranca.Prevista(m.MatriculaId, m.AlunoId, m.TurmaId, competencia, vencimento, m.Mensalidade,
                    primeira: comCobranca.Add(m.MatriculaId)));
            }
        }

        // Prevista automática de matrícula que saiu da lista e que ainda não venceu: a projeção dela deixou de existir.
        var ids = matriculas.Select(m => m.MatriculaId).ToHashSet();
        remover.AddRange(existentes.Where(c => !olhadas.Contains(c) && !ids.Contains(c.Matricula_ID)
                                               && c.Situacao == SituacaoCobranca.Prevista && !c.Confirmada && c.Vencimento >= hoje));
        return new PlanoCobrancas(criar, ajustadas, remover);
    }
}

public enum TipoEtapaRegua
{
    Antes = 10,
    NoDia = 20,
    Depois = 30
}

/// <summary>Um degrau da régua: "A3" (3 dias antes), "D0" (no dia), "V5" (5 dias depois).</summary>
public sealed record EtapaRegua(string Codigo, TipoEtapaRegua Tipo, int Dias, DateOnly Data, GatilhoMensagem Gatilho, TomMensagem Tom)
{
    public string Descricao => Tipo switch
    {
        TipoEtapaRegua.Antes => $"{Dias} dia{(Dias == 1 ? "" : "s")} antes do vencimento",
        TipoEtapaRegua.NoDia => "no dia do vencimento",
        _ => $"{Dias} dia{(Dias == 1 ? "" : "s")} depois do vencimento"
    };
}

/// <summary>
/// A régua de cobrança (BL-M06-04), com os degraus dos parâmetros da escolinha (lidos na hora, regra de ouro 14): lembretes
/// antes, no dia e cobranças depois do vencimento. O tom sobe: antes e no dia, cordial; depois do vencimento, a primeira
/// cordial e as seguintes diretas.
/// <para>
/// Sai no máximo um degrau por vez, o mais recente que já chegou e ainda não saiu: se a avaliação atrasar (sistema fora do
/// ar), a família recebe uma mensagem só, e não uma rajada. Nada sai de cobrança paga, cancelada, isenta ou com a fatura
/// numa conta anterior (CA-8).
/// </para>
/// </summary>
public static class ReguaCobranca
{
    public static IReadOnlyList<EtapaRegua> Etapas(DateOnly vencimento, ParametrosFinanceiro p)
    {
        var etapas = new List<EtapaRegua>();
        foreach (var d in p.LembretesDiasAntes.Where(d => d > 0).Distinct())
            etapas.Add(new EtapaRegua($"A{d}", TipoEtapaRegua.Antes, d, vencimento.AddDays(-d), GatilhoMensagem.MensalidadeAVencer, TomMensagem.Cordial));
        if (p.LembreteNoDia)
            etapas.Add(new EtapaRegua("D0", TipoEtapaRegua.NoDia, 0, vencimento, GatilhoMensagem.MensalidadeAVencer, TomMensagem.Cordial));
        var depois = p.CobrancasDiasDepois.Where(d => d > 0).Distinct().OrderBy(d => d).ToList();
        for (var i = 0; i < depois.Count; i++)
            etapas.Add(new EtapaRegua($"V{depois[i]}", TipoEtapaRegua.Depois, depois[i], vencimento.AddDays(depois[i]),
                GatilhoMensagem.MensalidadeVencida, i == 0 ? TomMensagem.Cordial : TomMensagem.Direto));
        return etapas.OrderBy(e => e.Data).ToList();
    }

    /// <summary>O degrau que deve sair hoje, ou <c>null</c>.</summary>
    public static EtapaRegua? Proxima(Cobranca cobranca, ParametrosFinanceiro p, DateOnly hoje)
    {
        if (!cobranca.EmAberto || cobranca.Sinal == SinalCobranca.ContaAnterior || string.IsNullOrWhiteSpace(cobranca.UrlFatura)) return null;
        var emitidaEm = cobranca.EmitidaEm is { } emitida ? DateOnly.FromDateTime(emitida) : hoje;
        return Etapas(cobranca.Vencimento, p)
            .Where(e => e.Data <= hoje && e.Data >= emitidaEm && (cobranca.UltimaEtapaData is not { } ultima || e.Data > ultima))
            .Where(e => e.Tipo == TipoEtapaRegua.Depois ? hoje > cobranca.Vencimento : hoje <= cobranca.Vencimento)
            .LastOrDefault();
    }
}
