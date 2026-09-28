using Microsoft.EntityFrameworkCore;
using MinhaEscolinha.Application.Portal;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Eventos;
using MinhaEscolinha.Domain.Financeiro;
using MinhaEscolinha.Infrastructure.Data.Contexto;

namespace MinhaEscolinha.Infrastructure.Portal;

/// <summary>As leituras do portal do responsável, sem rastreio, sempre pelos alunos que o serviço já filtrou.</summary>
public class PortalRepositorio(EscolinhaDbContext contexto) : IPortalRepositorio
{
    public async Task<IReadOnlyList<VinculoNoPortal>> VinculosAsync(Guid pessoaId, CancellationToken ct = default) =>
        await contexto.VinculosResponsavel.AsNoTracking()
            .Where(v => v.Responsavel!.Pessoa_ID == pessoaId)
            .Select(v => new VinculoNoPortal(v.Responsavel_ID, v.Responsavel!.Nome, v.Aluno_ID, v.Aluno!.Nome, v.Aluno.Situacao, v.Verificacao,
                v.Pagador || v.RecebeCobranca))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> TurmasAtivasAsync(IReadOnlyCollection<Guid> alunos, CancellationToken ct = default)
    {
        var ids = alunos.Distinct().ToList();
        var linhas = await contexto.Matriculas.AsNoTracking()
            .Where(m => ids.Contains(m.Aluno_ID) && m.Situacao == SituacaoMatricula.Ativa)
            .Select(m => new { m.Aluno_ID, m.Turma!.Nome })
            .ToListAsync(ct);
        return linhas.GroupBy(l => l.Aluno_ID)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(l => l.Nome).Order().ToList());
    }

    public async Task<IReadOnlyDictionary<Guid, DateTime>> ProximosTreinosAsync(IReadOnlyCollection<Guid> alunos, DateTime desde,
        CancellationToken ct = default)
    {
        var ids = alunos.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, DateTime>();
        var linhas = await (from m in contexto.Matriculas.AsNoTracking()
                            where ids.Contains(m.Aluno_ID) && m.Situacao == SituacaoMatricula.Ativa
                            join s in contexto.Sessoes.AsNoTracking() on m.Turma_ID equals s.Turma_ID
                            where s.Inicio >= desde && s.Situacao == Domain.Turmas.SituacaoSessao.Prevista
                            group s by m.Aluno_ID into g
                            select new { Aluno = g.Key, Inicio = g.Min(x => x.Inicio) })
            .ToListAsync(ct);
        return linhas.ToDictionary(l => l.Aluno, l => l.Inicio);
    }

    public async Task<IReadOnlyList<(Convocacao Convocacao, Evento Evento)>> ConvocacoesAsync(IReadOnlyCollection<Guid> alunos, DateTime desde,
        CancellationToken ct = default)
    {
        var ids = alunos.Distinct().ToList();
        var convocacoes = await (
            from c in contexto.Convocacoes.AsNoTracking()
            join e in contexto.Eventos.AsNoTracking() on c.Evento_ID equals e.Evento_ID
            // A escolhida e ainda não disparada é rascunho de quem convoca: a família não vê.
            where ids.Contains(c.Aluno_ID) && c.Situacao != SituacaoConvocacao.Selecionada && e.Inicio >= desde
            select c).ToListAsync(ct);
        if (convocacoes.Count == 0) return [];

        var eventoIds = convocacoes.Select(c => c.Evento_ID).Distinct().ToList();
        var eventos = await contexto.Eventos.AsNoTracking().Include(e => e.Turmas)
            .Where(e => eventoIds.Contains(e.Evento_ID)).ToDictionaryAsync(e => e.Evento_ID, ct);
        return convocacoes.Select(c => (c, eventos[c.Evento_ID])).OrderBy(l => l.Item2.Inicio).ToList();
    }

    public async Task<IReadOnlyList<Cobranca>> MensalidadesAsync(IReadOnlyCollection<Guid> alunos, DateOnly desdeCompetencia, CancellationToken ct = default)
    {
        var ids = alunos.Distinct().ToList();
        return await contexto.Cobrancas.AsNoTracking()
            .Where(c => ids.Contains(c.Aluno_ID)
                        && (c.Competencia >= desdeCompetencia || c.Situacao == SituacaoCobranca.Emitida || c.Situacao == SituacaoCobranca.Vencida))
            .OrderBy(c => c.Vencimento)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PresencaDoFilho>> PresencasAsync(IReadOnlyCollection<Guid> alunos, DateTime de, DateTime ate,
        CancellationToken ct = default)
    {
        var ids = alunos.Distinct().ToList();
        return await (
            from p in contexto.Presencas.AsNoTracking()
            join t in contexto.Turmas.AsNoTracking() on p.Turma_ID equals t.Turma_ID
            where ids.Contains(p.Aluno_ID) && p.SessaoInicio >= de && p.SessaoInicio < ate
            orderby p.SessaoInicio descending
            select new PresencaDoFilho(p.Aluno_ID, p.SessaoInicio, t.Nome, p.Situacao)).ToListAsync(ct);
    }
}
