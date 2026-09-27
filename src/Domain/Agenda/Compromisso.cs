using System.Globalization;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Equipe;

namespace MinhaEscolinha.Domain.Agenda;

public enum TipoCompromisso
{
    /// <summary>Sessão de treino gerada dos horários da turma (S1).</summary>
    Treino = 10,

    /// <summary>Evento com tipo — nesta versão, o jogo (S5).</summary>
    Evento = 20
}

/// <summary>
/// Compromisso datado (plano §3.6): o conceito comum a sessão de treino e evento, com início, fim, local e professor.
/// A ocupação de agenda do professor e da quadra (BL-M04-08) é uma consulta sobre os compromissos, não uma tabela.
/// </summary>
public sealed record Compromisso(
    TipoCompromisso Tipo,
    Guid Id,
    string Titulo,
    DateTime Inicio,
    DateTime Fim,
    Guid? LocalId,
    Guid? ProfessorId,
    Guid? TurmaId,
    bool Cancelado)
{
    public bool Sobrepoe(DateTime inicio, DateTime fim) => Inicio < fim && inicio < Fim;
}

public enum TipoConflito
{
    /// <summary>O professor já tem outro treino (ou jogo) no mesmo horário.</summary>
    ProfessorOcupado = 10,

    /// <summary>O horário não cabe em nenhuma faixa de disponibilidade do professor naquele dia.</summary>
    ForaDaDisponibilidade = 20,

    /// <summary>O professor registrou indisponibilidade pontual no período.</summary>
    ProfessorIndisponivel = 30,

    /// <summary>O local já recebe outro treino (ou jogo) no mesmo horário (P31, aprovado pelo responsável).</summary>
    LocalOcupado = 40
}

/// <summary>Um alerta de agenda. Sinaliza, não bloqueia (R9): quem marca decide seguir.</summary>
public sealed record ConflitoAgenda(TipoConflito Tipo, string Descricao);

/// <summary>Um horário semanal de outra turma: do mesmo professor ou no mesmo local.</summary>
public sealed record HorarioOcupado(string Turma, HorarioSemanal Horario);

/// <summary>
/// Alertas de agenda ao marcar treino. Regra pura, reaproveitada pelo evento (S5):
/// <list type="bullet">
/// <item>do professor (BL-M04-09, RF-CON-018): horário já ocupado por outra turma dele, fora da disponibilidade declarada ou
/// dentro de uma indisponibilidade pontual;</item>
/// <item>do local (P31): outra turma no mesmo local e horário. Uma quadra pode receber duas turmas ao mesmo tempo (meia quadra),
/// por isso é alerta, não bloqueio (R9).</item>
/// </list>
/// </summary>
public static class DetectorConflitos
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <param name="horarios">Os horários propostos para a turma.</param>
    /// <param name="outrasTurmas">Os horários das outras turmas ativas do mesmo professor.</param>
    /// <param name="ocorrencias">As sessões que os horários propostos vão gerar no horizonte (para as indisponibilidades pontuais).</param>
    public static IReadOnlyList<ConflitoAgenda> ParaHorarios(
        Professor professor,
        IReadOnlyCollection<HorarioSemanal> horarios,
        IReadOnlyCollection<HorarioOcupado> outrasTurmas,
        IEnumerable<(DateTime Inicio, DateTime Fim)> ocorrencias)
    {
        var conflitos = new List<ConflitoAgenda>();

        foreach (var horario in horarios.OrderBy(h => DiasSemana.Posicao(h.Dia)).ThenBy(h => h.Inicio))
        {
            foreach (var outra in outrasTurmas.Where(o => o.Horario.Sobrepoe(horario)))
                conflitos.Add(new ConflitoAgenda(TipoConflito.ProfessorOcupado,
                    $"{horario.Descricao()}: {professor.Nome} já dá aula na turma {outra.Turma} ({outra.Horario.Descricao()})."));

            if (professor.TemDisponibilidadeDeclarada
                && !professor.Disponibilidade.Any(f => f.Cobre(horario.Dia, horario.Inicio, horario.Fim)))
                conflitos.Add(new ConflitoAgenda(TipoConflito.ForaDaDisponibilidade,
                    $"{horario.Descricao()}: fora da disponibilidade de {professor.Nome} ({DescreverDisponibilidade(professor, horario.Dia)})."));
        }

        conflitos.AddRange(Indisponibilidades(professor, ocorrencias));
        return conflitos;
    }

    /// <summary>As ocorrências que caem numa indisponibilidade pontual do professor (uma linha por indisponibilidade).</summary>
    public static IEnumerable<ConflitoAgenda> Indisponibilidades(Professor professor, IEnumerable<(DateTime Inicio, DateTime Fim)> ocorrencias)
    {
        var lista = ocorrencias.OrderBy(o => o.Inicio).ToList();
        foreach (var indisponivel in professor.Indisponibilidades.OrderBy(i => i.Inicio))
        {
            var atingidas = lista.Where(o => o.Inicio < indisponivel.Fim && indisponivel.Inicio < o.Fim).ToList();
            if (atingidas.Count == 0) continue;
            var motivo = string.IsNullOrWhiteSpace(indisponivel.Motivo) ? string.Empty : $" ({indisponivel.Motivo})";
            var datas = string.Join(", ", atingidas.Select(o => o.Inicio.ToString("dd/MM HH:mm", PtBr)));
            yield return new ConflitoAgenda(TipoConflito.ProfessorIndisponivel,
                $"{professor.Nome} está indisponível de {indisponivel.Inicio.ToString("dd/MM HH:mm", PtBr)} a " +
                $"{indisponivel.Fim.ToString("dd/MM HH:mm", PtBr)}{motivo}: {Plural(atingidas.Count, "sessão", "sessões")} ({datas}).");
        }
    }

    /// <summary>Os horários propostos que batem com outra turma ativa no mesmo local (P31).</summary>
    /// <param name="local">O nome do local, para a mensagem.</param>
    /// <param name="horarios">Os horários propostos para a turma.</param>
    /// <param name="outrasNoLocal">Os horários das outras turmas ativas no mesmo local.</param>
    public static IEnumerable<ConflitoAgenda> LocalOcupado(string local, IReadOnlyCollection<HorarioSemanal> horarios,
        IReadOnlyCollection<HorarioOcupado> outrasNoLocal)
    {
        foreach (var horario in horarios.OrderBy(h => DiasSemana.Posicao(h.Dia)).ThenBy(h => h.Inicio))
        foreach (var outra in outrasNoLocal.Where(o => o.Horario.Sobrepoe(horario)))
            yield return new ConflitoAgenda(TipoConflito.LocalOcupado,
                $"{horario.Descricao()}: {local} já recebe a turma {outra.Turma} ({outra.Horario.Descricao()}).");
    }

    /// <summary>Os compromissos da lista que se sobrepõem a outro do mesmo professor (os cancelados não contam).</summary>
    public static IReadOnlySet<Guid> SobrepostosPorProfessor(IEnumerable<Compromisso> compromissos) =>
        Sobrepostos(compromissos, c => c.ProfessorId);

    /// <summary>Os compromissos da lista que se sobrepõem a outro no mesmo local (os cancelados não contam; P31).</summary>
    public static IReadOnlySet<Guid> SobrepostosPorLocal(IEnumerable<Compromisso> compromissos) =>
        Sobrepostos(compromissos, c => c.LocalId);

    private static HashSet<Guid> Sobrepostos(IEnumerable<Compromisso> compromissos, Func<Compromisso, Guid?> chave)
    {
        var ativos = compromissos.Where(c => !c.Cancelado && chave(c) is not null).ToList();
        var sobrepostos = new HashSet<Guid>();
        foreach (var grupo in ativos.GroupBy(chave))
        {
            var ordenados = grupo.OrderBy(c => c.Inicio).ToList();
            for (var i = 0; i < ordenados.Count; i++)
            for (var j = i + 1; j < ordenados.Count && ordenados[j].Inicio < ordenados[i].Fim; j++)
            {
                sobrepostos.Add(ordenados[i].Id);
                sobrepostos.Add(ordenados[j].Id);
            }
        }
        return sobrepostos;
    }

    public static string DescreverDisponibilidade(Professor professor, DayOfWeek dia)
    {
        var faixas = professor.Disponibilidade.Where(f => f.DiaSemana == dia).OrderBy(f => f.Inicio).ToList();
        return faixas.Count == 0
            ? $"sem disponibilidade {DiasSemana.ComArtigo(dia)}"
            : $"disponível {DiasSemana.ComArtigo(dia)}: " + string.Join(", ", faixas.Select(f => $"{f.Inicio:HH\\:mm}–{f.Fim:HH\\:mm}"));
    }

    private static string Plural(int n, string singular, string plural) => $"{n} {(n == 1 ? singular : plural)}";
}
