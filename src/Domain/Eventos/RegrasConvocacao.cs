namespace MinhaEscolinha.Domain.Eventos;

/// <summary>Os números do painel de confirmações (BL-M07-09), contra o mínimo de confirmados da turma.</summary>
public sealed record ContagemConvocacoes(int Selecionadas, int Aguardando, int Confirmadas, int Recusadas, int SemResposta, int Canceladas)
{
    public static readonly ContagemConvocacoes Zero = new(0, 0, 0, 0, 0, 0);

    /// <summary>As que já saíram para a família (canceladas de fora).</summary>
    public int Disparadas => Aguardando + Confirmadas + Recusadas + SemResposta;

    public static ContagemConvocacoes De(IEnumerable<SituacaoConvocacao> situacoes)
    {
        var lista = situacoes.ToList();
        return new ContagemConvocacoes(
            lista.Count(s => s == SituacaoConvocacao.Selecionada),
            lista.Count(s => s == SituacaoConvocacao.Enviada),
            lista.Count(s => s == SituacaoConvocacao.Confirmada),
            lista.Count(s => s == SituacaoConvocacao.Recusada),
            lista.Count(s => s == SituacaoConvocacao.SemResposta),
            lista.Count(s => s == SituacaoConvocacao.Cancelada));
    }
}

/// <summary>Um evento em que o aluno está convocado (não recusou, não foi cancelado).</summary>
public sealed record ConvocacaoDoAluno(Guid AlunoId, Guid EventoId, string Evento, DateTime Inicio, DateTime Fim);

/// <summary>Regras puras da convocação.</summary>
public static class RegrasConvocacao
{
    /// <summary>
    /// Os outros eventos do aluno em horário conflitante com este (BL-M07-06, RF-CON-010): sobreposição de horário.
    /// Sinaliza, não bloqueia (R9): o professor decide.
    /// </summary>
    public static IReadOnlyList<ConvocacaoDoAluno> Conflitos(Guid eventoId, DateTime inicio, DateTime fim, IEnumerable<ConvocacaoDoAluno> doAluno) =>
        doAluno.Where(c => c.EventoId != eventoId && c.Inicio < fim && inicio < c.Fim).OrderBy(c => c.Inicio).ToList();
}
