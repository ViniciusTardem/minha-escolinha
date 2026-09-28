using Microsoft.EntityFrameworkCore;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Presencas;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Presencas;
using MinhaEscolinha.Domain.Turmas;
using MinhaEscolinha.Infrastructure.Cadastro;
using MinhaEscolinha.Infrastructure.Data.Contexto;

namespace MinhaEscolinha.Infrastructure.Presencas;

public class PresencaRepositorio(EscolinhaDbContext contexto) : IPresencaRepositorio
{
    public async Task<IReadOnlyList<SessaoDaChamada>> SessoesAsync(DateOnly de, DateOnly ate, EscopoTurmas escopo, CancellationToken ct = default)
    {
        if (!escopo.Todas && escopo.Turmas.Count == 0) return [];
        var consulta = contexto.Sessoes.AsNoTracking().Where(s => s.Data >= de && s.Data <= ate);
        if (!escopo.Todas)
        {
            var turmas = escopo.Turmas.ToList();
            consulta = consulta.Where(s => turmas.Contains(s.Turma_ID));
        }
        return await consulta
            .Select(s => new SessaoDaChamada(s.SessaoTreino_ID, s.Data, s.Inicio, s.Fim, s.Turma!.Nome, s.Local!.Nome, s.Professor!.Nome,
                s.Situacao, contexto.Presencas.Count(p => p.SessaoTreino_ID == s.SessaoTreino_ID)))
            .ToListAsync(ct);
    }

    public Task<SessaoTreino?> ObterSessaoAsync(Guid id, CancellationToken ct = default) =>
        contexto.Sessoes.Include(s => s.Turma).Include(s => s.Local).Include(s => s.Professor)
            .FirstOrDefaultAsync(s => s.SessaoTreino_ID == id, ct);

    public async Task<IReadOnlyList<AlunoDaSessao>> AlunosDaSessaoAsync(SessaoTreino sessao, CancellationToken ct = default)
    {
        var data = sessao.Data;
        var linhas = await contexto.Matriculas.AsNoTracking()
            .Where(m => m.Turma_ID == sessao.Turma_ID && m.DataInicio <= data && (m.DataFim == null || m.DataFim >= data))
            .Select(m => new { m.Aluno_ID, m.Aluno!.Nome, m.Aluno.Situacao, m.Aluno.SituacaoDesde })
            .ToListAsync(ct);

        // Pausa e desligamento valem a partir da data da mudança: antes dela, o aluno continua no histórico da turma (CA-3).
        return linhas
            .DistinctBy(l => l.Aluno_ID)
            .Select(l => new AlunoDaSessao(l.Aluno_ID, l.Nome, l.Situacao == SituacaoAluno.Trial,
                !(l.Situacao is SituacaoAluno.Inativo or SituacaoAluno.Desistente && l.SituacaoDesde <= data)))
            .ToList();
    }

    public async Task<IReadOnlyList<Presenca>> PresencasDaSessaoAsync(Guid sessaoId, CancellationToken ct = default) =>
        await contexto.Presencas.Where(p => p.SessaoTreino_ID == sessaoId).ToListAsync(ct);

    public async Task<Dictionary<(Guid AlunoId, string Dispositivo), long>> MaioresSequenciasAsync(Guid sessaoId, CancellationToken ct = default) =>
        (await contexto.MarcacoesPresenca.AsNoTracking()
            .Where(m => m.SessaoTreino_ID == sessaoId && m.Resultado == ResultadoMarcacao.Aplicada)
            .GroupBy(m => new { m.Aluno_ID, m.Dispositivo })
            .Select(g => new { g.Key.Aluno_ID, g.Key.Dispositivo, Maior = g.Max(m => m.Sequencia) })
            .ToListAsync(ct))
        .ToDictionary(x => (x.Aluno_ID, x.Dispositivo), x => x.Maior);

    public async Task<IReadOnlySet<Guid>> MarcacoesRecebidasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return new HashSet<Guid>();
        var lista = ids.ToList();
        return (await contexto.MarcacoesPresenca.AsNoTracking()
                .Where(m => lista.Contains(m.Marcacao)).Select(m => m.Marcacao).ToListAsync(ct))
            .ToHashSet();
    }

    public async Task<IReadOnlyList<Presenca>> PresencasDoAlunoNaTurmaAsync(Guid alunoId, Guid turmaId, DateTime desde,
        CancellationToken ct = default) =>
        await contexto.Presencas.Where(p => p.Aluno_ID == alunoId && p.Turma_ID == turmaId && p.SessaoInicio >= desde).ToListAsync(ct);

    public Task<DateTime?> UltimoAlertaDisparadoAsync(Guid alunoId, CancellationToken ct = default) =>
        contexto.AlertasFaltas.AsNoTracking()
            .Where(a => a.Aluno_ID == alunoId && a.Situacao == SituacaoAlertaFaltas.Disparado)
            .OrderByDescending(a => a.AvaliadoEm)
            .Select(a => (DateTime?)a.AvaliadoEm)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<AlertaFaltas>> AlertasDisparadosAsync(Guid alunoId, Guid turmaId, DateTime desde, CancellationToken ct = default) =>
        await contexto.AlertasFaltas
            .Where(a => a.Aluno_ID == alunoId && a.Turma_ID == turmaId && a.Situacao == SituacaoAlertaFaltas.Disparado && a.SessaoInicio >= desde)
            .ToListAsync(ct);

    public Task<AlertaFaltas?> ObterAlertaAsync(Guid id, CancellationToken ct = default) =>
        contexto.AlertasFaltas.AsNoTracking().FirstOrDefaultAsync(a => a.AlertaFaltas_ID == id, ct);

    public async Task<IReadOnlyList<PresencaNaFicha>> PresencasDoAlunoAsync(Guid alunoId, int limite, CancellationToken ct = default) =>
        await (from p in contexto.Presencas.AsNoTracking()
               where p.Aluno_ID == alunoId
               join t in contexto.Turmas.AsNoTracking() on p.Turma_ID equals t.Turma_ID
               orderby p.SessaoInicio descending
               select new PresencaNaFicha(p.SessaoTreino_ID, p.SessaoInicio, p.Turma_ID, t.Nome, p.Situacao, p.Justificativa,
                   p.JustificativaSaude, true, p.MarcadaPorNome, p.MarcadaEm))
            .Take(limite)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<(Guid TurmaId, int? Meta, SituacaoPresenca Situacao)>> SituacoesDoAlunoAsync(Guid alunoId, DateTime desde,
        CancellationToken ct = default) =>
        (await (from p in contexto.Presencas.AsNoTracking()
                where p.Aluno_ID == alunoId && p.SessaoInicio >= desde
                join t in contexto.Turmas.AsNoTracking() on p.Turma_ID equals t.Turma_ID
                select new { p.Turma_ID, t.MetaFrequenciaPercentual, p.Situacao })
            .ToListAsync(ct))
        .Select(x => (x.Turma_ID, x.MetaFrequenciaPercentual, x.Situacao))
        .ToList();

    public async Task<IReadOnlyList<(AlertaFaltas Alerta, string Turma)>> AlertasDoAlunoAsync(Guid alunoId, int limite, CancellationToken ct = default) =>
        (await (from a in contexto.AlertasFaltas.AsNoTracking()
                where a.Aluno_ID == alunoId
                join t in contexto.Turmas.AsNoTracking() on a.Turma_ID equals t.Turma_ID
                orderby a.AvaliadoEm descending
                select new { Alerta = a, t.Nome })
            .Take(limite)
            .ToListAsync(ct))
        .Select(x => (x.Alerta, x.Nome))
        .ToList();

    public async Task<IReadOnlyList<(Guid AlunoId, string Nome, int? Meta, SituacaoPresenca Situacao)>> SituacoesDesdeAsync(DateTime desde, EscopoTurmas escopo,
        CancellationToken ct = default)
    {
        if (!escopo.Todas && escopo.Turmas.Count == 0) return [];
        var consulta = from p in contexto.Presencas.AsNoTracking()
                       where p.SessaoInicio >= desde
                       join a in contexto.Alunos.AsNoTracking() on p.Aluno_ID equals a.Aluno_ID
                       where a.Situacao == SituacaoAluno.Trial || a.Situacao == SituacaoAluno.Ativo
                       join t in contexto.Turmas.AsNoTracking() on p.Turma_ID equals t.Turma_ID
                       select new { p.Turma_ID, p.Aluno_ID, a.Nome, t.MetaFrequenciaPercentual, p.Situacao };
        if (!escopo.Todas)
        {
            var turmas = escopo.Turmas.ToList();
            consulta = consulta.Where(x => turmas.Contains(x.Turma_ID));
        }
        return (await consulta.ToListAsync(ct)).Select(x => (x.Aluno_ID, x.Nome, x.MetaFrequenciaPercentual, x.Situacao)).ToList();
    }

    public void AdicionarPresenca(Presenca presenca) => contexto.Presencas.Add(presenca);
    public void AdicionarMarcacao(MarcacaoPresenca marcacao) => contexto.MarcacoesPresenca.Add(marcacao);
    public void AdicionarAlerta(AlertaFaltas alerta) => contexto.AlertasFaltas.Add(alerta);

    public Task SalvarAsync(CancellationToken ct = default) => Conflitos.SalvarAsync(contexto, ct);
}
