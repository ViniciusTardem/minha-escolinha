using System.Globalization;
using MinhaEscolinha.Domain.Configuracao.Parametros;

namespace MinhaEscolinha.Domain.Presencas;

/// <summary>O que fazer com uma marcação que chegou.</summary>
public sealed record DecisaoMarcacao(bool Aplicar, string? Motivo)
{
    public static readonly DecisaoMarcacao Aplica = new(true, null);
}

/// <summary>
/// O registro da quadra prevalece (R10, CA-21). A marcação nova vale sobre o que a presença tinha antes, venha do mesmo
/// aparelho ou de outro. A única exceção é a marcação atrasada do <b>mesmo</b> aparelho: com sequência menor ou igual à
/// maior que ele já aplicou para aquele aluno naquela sessão, ela é mais antiga e não desfaz nada (nem a correção feita
/// depois em outro aparelho). O reenvio da mesma marcação (mesmo id) nem chega aqui: o serviço reconhece o id.
/// </summary>
public static class RegraMarcacao
{
    /// <param name="maiorSequenciaDoAparelho">A maior sequência já aplicada por este aparelho para o aluno nesta sessão.</param>
    public static DecisaoMarcacao Decidir(NovaMarcacao nova, long? maiorSequenciaDoAparelho) =>
        maiorSequenciaDoAparelho is { } maior && nova.Sequencia <= maior
            ? new DecisaoMarcacao(false, "Este aparelho já tinha mandado uma marcação mais nova para este aluno.")
            : DecisaoMarcacao.Aplica;
}

/// <summary>Uma presença como a regra de faltas a enxerga: quando foi a sessão e o que foi marcado.</summary>
public sealed record PresencaContada(DateTime Inicio, SituacaoPresenca Situacao);

/// <summary>A regra foi atingida: quantas faltas contaram e o período ("setembro" ou "sequência").</summary>
public sealed record RegraAtingida(int Faltas, string Periodo);

/// <summary>
/// A regra do alerta de faltas (BL-M05-08, parâmetros do BL-M05-07), avaliada no registro de cada falta:
/// <list type="bullet">
/// <item><b>no mês:</b> as faltas do aluno na turma no mês da sessão chegam ao limite (CA-4: a 3ª falta do mês dispara);</item>
/// <item><b>em sequência:</b> as faltas seguidas do aluno na turma, da sessão avaliada para trás, chegam ao limite. A
/// presença interrompe a sequência; a falta justificada que não conta é pulada (nem soma, nem interrompe).</item>
/// </list>
/// A falta justificada só conta se a escolinha ligar (<c>presenca.falta_justificada.conta_para_alerta</c>, P04).
/// </summary>
public static class AvaliadorFaltas
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public static bool Conta(SituacaoPresenca situacao, bool contaJustificada) =>
        situacao == SituacaoPresenca.Falta || (contaJustificada && situacao == SituacaoPresenca.Justificada);

    /// <param name="alvo">A presença avaliada (a falta que acabou de ser registrada).</param>
    /// <param name="presencas">As presenças do aluno na turma (inclui a avaliada).</param>
    public static RegraAtingida? Avaliar(RegraFaltas regra, int limite, bool contaJustificada, PresencaContada alvo,
        IEnumerable<PresencaContada> presencas)
    {
        if (!Conta(alvo.Situacao, contaJustificada)) return null;
        var lista = presencas.ToList();

        if (regra == RegraFaltas.NoMes)
        {
            var noMes = lista.Count(p => p.Inicio.Year == alvo.Inicio.Year && p.Inicio.Month == alvo.Inicio.Month
                                         && Conta(p.Situacao, contaJustificada));
            return noMes >= limite ? new RegraAtingida(noMes, NomeDoMes(alvo.Inicio)) : null;
        }

        var seguidas = 0;
        foreach (var p in lista.Where(p => p.Inicio <= alvo.Inicio).OrderByDescending(p => p.Inicio))
        {
            if (Conta(p.Situacao, contaJustificada)) seguidas++;
            else if (p.Situacao == SituacaoPresenca.Presente) break;
        }
        return seguidas >= limite ? new RegraAtingida(seguidas, "sequência") : null;
    }

    /// <summary>
    /// O alerta ainda se sustenta com as presenças de agora? Se uma falta foi corrigida depois, a regra pode ter deixado de
    /// valer (com o limite do momento em que o alerta saiu).
    /// </summary>
    public static bool AindaVale(AlertaFaltas alerta, bool contaJustificada, IEnumerable<PresencaContada> presencas)
    {
        var lista = presencas.ToList();
        var alvo = lista.FirstOrDefault(p => p.Inicio == alerta.SessaoInicio);
        if (alerta.Regra == RegraFaltas.NoMes)
            return lista.Count(p => p.Inicio.Year == alerta.SessaoInicio.Year && p.Inicio.Month == alerta.SessaoInicio.Month
                                    && Conta(p.Situacao, contaJustificada)) >= alerta.Limite;
        return alvo is not null && Avaliar(alerta.Regra, alerta.Limite, contaJustificada, alvo, lista) is not null;
    }

    /// <summary>O mesmo aluno não gera dois alertas dentro da janela (CA-5): conta desde o último alerta disparado.</summary>
    public static bool DentroDaJanela(DateTime? ultimoDisparo, DateTime agora, int janelaDias) =>
        ultimoDisparo is { } ultimo && agora < ultimo.AddDays(janelaDias);

    public static string NomeDoMes(DateTime data) => data.ToString("MMMM", PtBr);
}

/// <summary>A frequência de um aluno numa janela: presenças sobre as sessões consideradas.</summary>
public sealed record Frequencia(int Presentes, int Consideradas)
{
    public decimal? Percentual => Consideradas == 0 ? null : Math.Round(100m * Presentes / Consideradas, 1);
}

/// <summary>
/// O risco de evasão (BL-M05-11): a frequência na janela configurada, abaixo do percentual mínimo, destaca o aluno.
/// Sinaliza, não bloqueia (R9). As sessões consideradas são as marcadas; a falta justificada entra só se a escolinha
/// contar a justificada no alerta (o mesmo parâmetro, P04).
/// </summary>
public static class CalculadoraFrequencia
{
    public static Frequencia Calcular(IEnumerable<SituacaoPresenca> situacoes, bool contaJustificada)
    {
        var presentes = 0;
        var consideradas = 0;
        foreach (var s in situacoes)
        {
            if (s == SituacaoPresenca.Presente) presentes++;
            if (s != SituacaoPresenca.Justificada || contaJustificada) consideradas++;
        }
        return new Frequencia(presentes, consideradas);
    }

    public static bool EmRisco(Frequencia frequencia, decimal minimoPercentual) =>
        frequencia.Percentual is { } percentual && percentual < minimoPercentual;
}
