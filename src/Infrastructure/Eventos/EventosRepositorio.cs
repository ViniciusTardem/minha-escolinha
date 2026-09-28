using Microsoft.EntityFrameworkCore;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Eventos;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Domain.Agenda;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Eventos;
using MinhaEscolinha.Domain.Financeiro;
using MinhaEscolinha.Infrastructure.Cadastro;
using MinhaEscolinha.Infrastructure.Data.Contexto;

namespace MinhaEscolinha.Infrastructure.Eventos;

public class EventosRepositorio(EscolinhaDbContext contexto) : IEventosRepositorio
{
    public void Adicionar(Evento evento) => contexto.Eventos.Add(evento);
    public void AdicionarConvocacao(Convocacao convocacao) => contexto.Convocacoes.Add(convocacao);
    public void RemoverConvocacao(Convocacao convocacao) => contexto.Convocacoes.Remove(convocacao);

    public Task<Evento?> ObterAsync(Guid id, CancellationToken ct = default) =>
        contexto.Eventos.Include(e => e.Turmas).FirstOrDefaultAsync(e => e.Evento_ID == id, ct);

    public async Task<IReadOnlyList<Evento>> ListarAsync(DateTime de, DateTime ate, EscopoTurmas alcance, CancellationToken ct = default)
    {
        var consulta = contexto.Eventos.AsNoTracking().Include(e => e.Turmas).Where(e => e.Inicio >= de && e.Inicio < ate);
        if (!alcance.Todas)
        {
            var turmas = alcance.Turmas.ToList();
            consulta = consulta.Where(e => e.Turmas.Any(t => turmas.Contains(t.Turma_ID)));
        }
        return await consulta.OrderBy(e => e.Inicio).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Convocacao>> ConvocacoesAsync(IReadOnlyCollection<Guid> eventos, CancellationToken ct = default)
    {
        if (eventos.Count == 0) return [];
        var ids = eventos.Distinct().ToList();
        return await contexto.Convocacoes.Where(c => ids.Contains(c.Evento_ID)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Convocacao>> ConvocacoesComHistoricoAsync(IReadOnlyCollection<Guid> eventos, CancellationToken ct = default)
    {
        if (eventos.Count == 0) return [];
        var ids = eventos.Distinct().ToList();
        return await contexto.Convocacoes.AsNoTracking().Include(c => c.Historico).AsSplitQuery()
            .Where(c => ids.Contains(c.Evento_ID)).ToListAsync(ct);
    }

    public Task<Convocacao?> ObterConvocacaoAsync(Guid id, CancellationToken ct = default) =>
        contexto.Convocacoes.FirstOrDefaultAsync(c => c.Convocacao_ID == id, ct);

    public async Task<IReadOnlyList<AlunoConvocavel>> AlunosConvocaveisAsync(IReadOnlyCollection<Guid> turmas, DateOnly data,
        CancellationToken ct = default)
    {
        if (turmas.Count == 0) return [];
        var ids = turmas.Distinct().ToList();
        return await (
            from m in contexto.Matriculas.AsNoTracking()
            where ids.Contains(m.Turma_ID) && m.DataInicio <= data && (m.DataFim == null || m.DataFim >= data)
                  && (m.Aluno!.Situacao == SituacaoAluno.Trial || m.Aluno.Situacao == SituacaoAluno.Ativo)
            select new AlunoConvocavel(m.Aluno_ID, m.Aluno!.Nome, m.Turma_ID, m.Aluno.Situacao == SituacaoAluno.Trial,
                contexto.VinculosResponsavel.Any(v => v.Aluno_ID == m.Aluno_ID && v.RecebeNotificacao && v.Responsavel!.Telefone != null)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ConvocacaoDoAluno>> ConvocacoesAtivasAsync(IReadOnlyCollection<Guid> alunos, DateTime de, DateTime ate,
        CancellationToken ct = default)
    {
        if (alunos.Count == 0) return [];
        var ids = alunos.Distinct().ToList();
        var linhas = await (
            from c in contexto.Convocacoes.AsNoTracking()
            join e in contexto.Eventos.AsNoTracking() on c.Evento_ID equals e.Evento_ID
            where ids.Contains(c.Aluno_ID)
                  && (c.Situacao == SituacaoConvocacao.Selecionada || c.Situacao == SituacaoConvocacao.Enviada
                      || c.Situacao == SituacaoConvocacao.Confirmada || c.Situacao == SituacaoConvocacao.SemResposta)
                  && (e.Situacao == SituacaoEvento.Agendado || e.Situacao == SituacaoEvento.Confirmado)
                  && e.Inicio >= de && e.Inicio < ate
            select new { c.Aluno_ID, e.Evento_ID, e.Tipo, e.Adversario, e.Inicio, e.DuracaoMinutos }).ToListAsync(ct);
        return linhas
            .Select(l => new ConvocacaoDoAluno(l.Aluno_ID, l.Evento_ID,
                PoliticaPerfil.Rotulo(l.Tipo) + (l.Adversario is null ? string.Empty : $" contra {l.Adversario}"),
                l.Inicio, l.Inicio.AddMinutes(l.DuracaoMinutos)))
            .ToList();
    }

    public async Task<IReadOnlySet<Guid>> InadimplentesAsync(IReadOnlyCollection<Guid> alunos, DateOnly vencidasAte, CancellationToken ct = default)
    {
        if (alunos.Count == 0) return new HashSet<Guid>();
        var ids = alunos.Distinct().ToList();
        return (await contexto.Cobrancas.AsNoTracking()
                .Where(c => ids.Contains(c.Aluno_ID) && c.Situacao == SituacaoCobranca.Vencida && c.Vencimento <= vencidasAte)
                .Select(c => c.Aluno_ID).Distinct().ToListAsync(ct))
            .ToHashSet();
    }

    public async Task<IReadOnlyList<Evento>> ComPendentesAteAsync(DateTime ate, CancellationToken ct = default) =>
        await contexto.Eventos.Include(e => e.Turmas)
            .Where(e => (e.Situacao == SituacaoEvento.Agendado || e.Situacao == SituacaoEvento.Confirmado) && e.Inicio <= ate
                        && contexto.Convocacoes.Any(c => c.Evento_ID == e.Evento_ID && c.Situacao == SituacaoConvocacao.Enviada))
            .OrderBy(e => e.Inicio)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Convocacao>> ConvocacoesFuturasDoAlunoAsync(Guid alunoId, Guid? turmaId, DateTime aPartirDe,
        CancellationToken ct = default) =>
        await (
            from c in contexto.Convocacoes
            join e in contexto.Eventos on c.Evento_ID equals e.Evento_ID
            where c.Aluno_ID == alunoId && (turmaId == null || c.Turma_ID == turmaId) && c.Situacao != SituacaoConvocacao.Cancelada
                  && (e.Situacao == SituacaoEvento.Agendado || e.Situacao == SituacaoEvento.Confirmado) && e.Inicio >= aPartirDe
            select c).ToListAsync(ct);

    public async Task<IReadOnlyList<Compromisso>> CompromissosAsync(DateTime de, DateTime ate, Guid? professorId, Guid? localId,
        CancellationToken ct = default)
    {
        var lista = await contexto.Eventos.AsNoTracking().Include(e => e.Turmas)
            .Where(e => e.Inicio < ate && e.Inicio.AddMinutes(e.DuracaoMinutos) > de)
            .Where(e => professorId == null || e.Professor_ID == professorId)
            .Where(e => localId == null || e.Local_ID == localId)
            .ToListAsync(ct);
        if (lista.Count == 0) return [];
        var nomes = await NomesDasTurmasAsync(lista.SelectMany(e => e.IdsDasTurmas).Distinct().ToList(), ct);
        return lista
            .Select(e => new Compromisso(TipoCompromisso.Evento, e.Evento_ID,
                e.Titulo(EmissorGatilhos.Juntar(e.IdsDasTurmas.Select(t => nomes.GetValueOrDefault(t, "—")).OrderBy(n => n))),
                e.Inicio, e.Fim, e.Local_ID, e.Professor_ID, e.IdsDasTurmas.FirstOrDefault(), e.Encerrado))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, string>> NomesDosAlunosAsync(IReadOnlyCollection<Guid> alunos, CancellationToken ct = default)
    {
        if (alunos.Count == 0) return new Dictionary<Guid, string>();
        var ids = alunos.Distinct().ToList();
        return await contexto.Alunos.AsNoTracking().Where(a => ids.Contains(a.Aluno_ID)).ToDictionaryAsync(a => a.Aluno_ID, a => a.Nome, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> NomesDasTurmasAsync(IReadOnlyCollection<Guid> turmas, CancellationToken ct = default)
    {
        if (turmas.Count == 0) return new Dictionary<Guid, string>();
        var ids = turmas.Distinct().ToList();
        return await contexto.Turmas.AsNoTracking().Where(t => ids.Contains(t.Turma_ID)).ToDictionaryAsync(t => t.Turma_ID, t => t.Nome, ct);
    }

    public Task SalvarAsync(CancellationToken ct = default) => Conflitos.SalvarAsync(contexto, ct);
}
