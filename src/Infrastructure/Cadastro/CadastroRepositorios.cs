using Microsoft.EntityFrameworkCore;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Equipe;
using MinhaEscolinha.Domain.Turmas;
using MinhaEscolinha.Infrastructure.Data.Contexto;

namespace MinhaEscolinha.Infrastructure.Cadastro;

// Fatia S1. Tudo aqui passa pelo filtro de escolinha: nenhuma consulta ignora o filtro.

internal static class Conflitos
{
    /// <summary>Salva traduzindo o conflito de versão de linha para a exceção da aplicação (D27).</summary>
    public static async Task SalvarAsync(EscolinhaDbContext contexto, CancellationToken ct)
    {
        try
        {
            await contexto.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflitoDeConcorrenciaException(Cadastros.MensagemConflito, ex);
        }
    }
}

public class EquipeRepositorio(EscolinhaDbContext contexto) : IEquipeRepositorio
{
    public async Task<IReadOnlyList<Professor>> ListarProfessoresAsync(CancellationToken ct = default) =>
        await contexto.Professores.AsNoTracking().Include(p => p.Disponibilidade).OrderBy(p => p.NomeBusca).ToListAsync(ct);

    public Task<Professor?> ObterProfessorAsync(Guid id, CancellationToken ct = default) =>
        contexto.Professores.Include(p => p.Disponibilidade).Include(p => p.Indisponibilidades)
            .AsSplitQuery().FirstOrDefaultAsync(p => p.Professor_ID == id, ct);

    public Task<bool> CpfEmUsoAsync(string cpf, Guid? excetoId, CancellationToken ct = default) =>
        contexto.Professores.AnyAsync(p => p.Cpf == cpf && (excetoId == null || p.Professor_ID != excetoId), ct);

    public Task<Professor?> ProfessorDaPessoaAsync(Guid pessoaId, CancellationToken ct = default) =>
        contexto.Professores.AsNoTracking().FirstOrDefaultAsync(p => p.Pessoa_ID == pessoaId, ct);

    public async Task<IReadOnlyList<Professor>> ProfessoresComAgendaAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0
            ? []
            : await contexto.Professores.AsNoTracking().Include(p => p.Disponibilidade).Include(p => p.Indisponibilidades)
                .AsSplitQuery().Where(p => ids.Contains(p.Professor_ID)).ToListAsync(ct);

    public void AdicionarProfessor(Professor professor) => contexto.Professores.Add(professor);
    public void AdicionarFaixa(FaixaDisponibilidade faixa) => contexto.Set<FaixaDisponibilidade>().Add(faixa);
    public void AdicionarIndisponibilidade(IndisponibilidadeProfessor indisponibilidade) => contexto.Set<IndisponibilidadeProfessor>().Add(indisponibilidade);
    public void RemoverFaixa(FaixaDisponibilidade faixa) => contexto.Set<FaixaDisponibilidade>().Remove(faixa);
    public void RemoverIndisponibilidade(IndisponibilidadeProfessor indisponibilidade) => contexto.Set<IndisponibilidadeProfessor>().Remove(indisponibilidade);
    public Task SalvarAsync(CancellationToken ct = default) => Conflitos.SalvarAsync(contexto, ct);
}

public class TurmasRepositorio(EscolinhaDbContext contexto) : ITurmasRepositorio
{
    public async Task<IReadOnlyList<Local>> ListarLocaisAsync(CancellationToken ct = default) =>
        await contexto.Locais.AsNoTracking().Include(l => l.Horarios).OrderBy(l => l.Nome).ToListAsync(ct);

    public Task<Local?> ObterLocalAsync(Guid id, CancellationToken ct = default) =>
        contexto.Locais.Include(l => l.Horarios).FirstOrDefaultAsync(l => l.Local_ID == id, ct);

    public Task<bool> NomeLocalEmUsoAsync(string nome, Guid? excetoId, CancellationToken ct = default) =>
        contexto.Locais.AnyAsync(l => l.Nome == nome && (excetoId == null || l.Local_ID != excetoId), ct);

    public void AdicionarLocal(Local local) => contexto.Locais.Add(local);
    public void AdicionarFaixaLocal(FaixaHorarioLocal faixa) => contexto.Set<FaixaHorarioLocal>().Add(faixa);
    public void RemoverFaixaLocal(FaixaHorarioLocal faixa) => contexto.Set<FaixaHorarioLocal>().Remove(faixa);

    public async Task<IReadOnlyList<Turma>> ListarTurmasAsync(EscopoTurmas escopo, bool incluirEncerradas, CancellationToken ct = default)
    {
        if (!escopo.Todas && escopo.Turmas.Count == 0) return [];
        var consulta = contexto.Turmas.AsNoTracking().Include(t => t.Horarios).Include(t => t.Local).Include(t => t.Professor)
            .AsSplitQuery()
            .Where(t => incluirEncerradas || t.Situacao == SituacaoTurma.Ativa);
        if (!escopo.Todas)
        {
            var ids = escopo.Turmas.ToList();
            consulta = consulta.Where(t => ids.Contains(t.Turma_ID));
        }
        return await consulta.OrderBy(t => t.Nome).ToListAsync(ct);
    }

    public Task<Turma?> ObterTurmaAsync(Guid id, CancellationToken ct = default) =>
        contexto.Turmas.Include(t => t.Horarios).Include(t => t.Local).Include(t => t.Professor)
            .AsSplitQuery().FirstOrDefaultAsync(t => t.Turma_ID == id, ct);

    public Task<bool> NomeTurmaEmUsoAsync(string nome, Guid? excetoId, CancellationToken ct = default) =>
        contexto.Turmas.AnyAsync(t => t.Nome == nome && t.Situacao == SituacaoTurma.Ativa && (excetoId == null || t.Turma_ID != excetoId), ct);

    public async Task<IReadOnlyList<Turma>> TurmasAtivasDoProfessorAsync(Guid professorId, Guid? excetoTurmaId, CancellationToken ct = default) =>
        await contexto.Turmas.AsNoTracking().Include(t => t.Horarios)
            .Where(t => t.Professor_ID == professorId && t.Situacao == SituacaoTurma.Ativa && (excetoTurmaId == null || t.Turma_ID != excetoTurmaId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Turma>> TurmasAtivasDoLocalAsync(Guid localId, Guid? excetoTurmaId, CancellationToken ct = default) =>
        await contexto.Turmas.AsNoTracking().Include(t => t.Horarios)
            .Where(t => t.Local_ID == localId && t.Situacao == SituacaoTurma.Ativa && (excetoTurmaId == null || t.Turma_ID != excetoTurmaId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> IdsTurmasAtivasAsync(CancellationToken ct = default) =>
        await contexto.Turmas.Where(t => t.Situacao == SituacaoTurma.Ativa).Select(t => t.Turma_ID).ToListAsync(ct);

    public void AdicionarTurma(Turma turma) => contexto.Turmas.Add(turma);
    public void AdicionarHorario(HorarioTurma horario) => contexto.HorariosTurma.Add(horario);
    public void RemoverHorario(HorarioTurma horario) => contexto.HorariosTurma.Remove(horario);

    public void DefinirVersaoOriginal(Turma turma, byte[] versao) =>
        contexto.Entry(turma).Property(t => t.Versao).OriginalValue = versao;

    public async Task<IReadOnlyDictionary<Guid, int>> OcupacaoAsync(CancellationToken ct = default) =>
        await contexto.Matriculas.Where(m => m.Situacao == SituacaoMatricula.Ativa)
            .GroupBy(m => m.Turma_ID)
            .Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);

    public async Task<IReadOnlyList<SessaoTreino>> SessoesDaTurmaDesdeAsync(Guid turmaId, DateOnly desde, CancellationToken ct = default) =>
        await contexto.Sessoes.Include(s => s.Turma).Include(s => s.Local).Include(s => s.Professor)
            .Where(s => s.Turma_ID == turmaId && s.Data >= desde)
            .OrderBy(s => s.Inicio)
            .ToListAsync(ct);

    public Task<SessaoTreino?> ObterSessaoAsync(Guid id, CancellationToken ct = default) =>
        contexto.Sessoes.Include(s => s.Turma).Include(s => s.Local).Include(s => s.Professor)
            .FirstOrDefaultAsync(s => s.SessaoTreino_ID == id, ct);

    public async Task<IReadOnlyList<SessaoTreino>> ListarSessoesAsync(DateOnly de, DateOnly ate, CancellationToken ct = default) =>
        await contexto.Sessoes.AsNoTracking().Include(s => s.Turma).Include(s => s.Local).Include(s => s.Professor)
            .Where(s => s.Data >= de && s.Data <= ate)
            .OrderBy(s => s.Inicio)
            .ToListAsync(ct);

    public void AdicionarSessao(SessaoTreino sessao) => contexto.Sessoes.Add(sessao);
    public void RemoverSessao(SessaoTreino sessao) => contexto.Sessoes.Remove(sessao);
    public Task SalvarAsync(CancellationToken ct = default) => Conflitos.SalvarAsync(contexto, ct);
}

public class AlunosRepositorio(EscolinhaDbContext contexto) : IAlunosRepositorio
{
    public async Task<Pagina<AlunoNaLista>> ListarAsync(FiltroAlunos filtro, EscopoTurmas escopo, IReadOnlyCollection<Guid>? somenteIds, int pagina,
        int tamanho, CancellationToken ct = default)
    {
        if (!escopo.Todas && escopo.Turmas.Count == 0) return new Pagina<AlunoNaLista>([], 0, pagina, tamanho);

        var consulta = contexto.Alunos.AsNoTracking();
        if (somenteIds is not null)
        {
            var ids = somenteIds.ToList();
            consulta = consulta.Where(a => ids.Contains(a.Aluno_ID));
        }
        if (!escopo.Todas)
        {
            // Professor: só os alunos com matrícula ativa numa turma dele (BL-M08-05).
            var ids = escopo.Turmas.ToList();
            consulta = consulta.Where(a => contexto.Matriculas.Any(m => m.Aluno_ID == a.Aluno_ID && m.Situacao == SituacaoMatricula.Ativa
                                                                       && ids.Contains(m.Turma_ID)));
        }
        if (filtro.TurmaId is { } turmaId)
            consulta = consulta.Where(a => contexto.Matriculas.Any(m => m.Aluno_ID == a.Aluno_ID && m.Turma_ID == turmaId
                                                                       && m.Situacao == SituacaoMatricula.Ativa));
        consulta = filtro.Situacao switch
        {
            FiltroSituacaoAluno.NaEscolinha => consulta.Where(a => a.Situacao != SituacaoAluno.Desistente),
            FiltroSituacaoAluno.Todos => consulta,
            var s => consulta.Where(a => a.Situacao == (SituacaoAluno)(int)s)
        };
        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            var termo = TextoHelper.NormalizarParaBusca(filtro.Busca);
            var digitos = TextoHelper.SomenteDigitos(filtro.Busca);
            consulta = consulta.Where(a => a.NomeBusca.Contains(termo)
                                           || (digitos.Length >= 3 && a.Cpf != null && a.Cpf.Contains(digitos))
                                           || contexto.VinculosResponsavel.Any(v => v.Aluno_ID == a.Aluno_ID
                                                                                 && v.Responsavel!.NomeBusca.Contains(termo)));
        }

        var total = await consulta.CountAsync(ct);
        // Uma consulta só: a página é pequena, e cada aluno tem poucas turmas e um ou dois responsáveis.
        var linhas = await consulta.OrderBy(a => a.NomeBusca)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .AsSingleQuery()
            .Select(a => new
            {
                a.Aluno_ID, a.Nome, a.DataNascimento, a.Categoria, a.Situacao, a.TrialAte,
                Turmas = contexto.Matriculas.Where(m => m.Aluno_ID == a.Aluno_ID && m.Situacao == SituacaoMatricula.Ativa)
                    .Select(m => m.Turma!.Nome).ToList(),
                Responsaveis = contexto.VinculosResponsavel.Where(v => v.Aluno_ID == a.Aluno_ID)
                    .OrderByDescending(v => v.Pagador).Select(v => v.Responsavel!.Nome).ToList()
            })
            .ToListAsync(ct);

        var itens = linhas.Select(l => new AlunoNaLista(l.Aluno_ID, l.Nome, l.DataNascimento, l.Categoria, l.Situacao, l.TrialAte,
            l.Turmas.OrderBy(n => n).ToList(), l.Responsaveis)).ToList();
        return new Pagina<AlunoNaLista>(itens, total, pagina, tamanho);
    }

    public Task<Aluno?> ObterAlunoAsync(Guid id, CancellationToken ct = default) =>
        contexto.Alunos.FirstOrDefaultAsync(a => a.Aluno_ID == id, ct);

    public Task<Aluno?> AlunoPorCpfAsync(string cpf, CancellationToken ct = default) =>
        contexto.Alunos.AsNoTracking().FirstOrDefaultAsync(a => a.Cpf == cpf, ct);

    public async Task<IReadOnlyList<Aluno>> AlunosComMesmoNomeENascimentoAsync(string nomeBusca, DateOnly nascimento, CancellationToken ct = default) =>
        await contexto.Alunos.AsNoTracking().Where(a => a.NomeBusca == nomeBusca && a.DataNascimento == nascimento).ToListAsync(ct);

    public void AdicionarAluno(Aluno aluno) => contexto.Alunos.Add(aluno);

    public void DefinirVersaoOriginal(Aluno aluno, byte[] versao) =>
        contexto.Entry(aluno).Property(a => a.Versao).OriginalValue = versao;

    public async Task<IReadOnlyList<(Guid Id, string Nome, DateOnly? TrialAte)>> TrialsAsync(EscopoTurmas escopo, CancellationToken ct = default)
    {
        if (!escopo.Todas && escopo.Turmas.Count == 0) return [];
        var consulta = contexto.Alunos.AsNoTracking().Where(a => a.Situacao == SituacaoAluno.Trial);
        if (!escopo.Todas)
        {
            var ids = escopo.Turmas.ToList();
            consulta = consulta.Where(a => contexto.Matriculas.Any(m => m.Aluno_ID == a.Aluno_ID && m.Situacao == SituacaoMatricula.Ativa
                                                                       && ids.Contains(m.Turma_ID)));
        }
        var linhas = await consulta.Select(a => new { a.Aluno_ID, a.Nome, a.TrialAte }).ToListAsync(ct);
        return linhas.Select(l => (l.Aluno_ID, l.Nome, l.TrialAte)).ToList();
    }

    public async Task<IReadOnlyList<VinculoAlunoResponsavel>> VinculosDoAlunoAsync(Guid alunoId, CancellationToken ct = default) =>
        await contexto.VinculosResponsavel.Include(v => v.Responsavel).Where(v => v.Aluno_ID == alunoId).ToListAsync(ct);

    public Task<VinculoAlunoResponsavel?> ObterVinculoAsync(Guid id, CancellationToken ct = default) =>
        contexto.VinculosResponsavel.Include(v => v.Responsavel).Include(v => v.Aluno)
            .FirstOrDefaultAsync(v => v.VinculoAlunoResponsavel_ID == id, ct);

    public async Task<IReadOnlyList<VinculoAlunoResponsavel>> VinculosDoResponsavelAsync(Guid responsavelId, CancellationToken ct = default) =>
        await contexto.VinculosResponsavel.Include(v => v.Aluno).Where(v => v.Responsavel_ID == responsavelId).ToListAsync(ct);

    public void AdicionarVinculo(VinculoAlunoResponsavel vinculo) => contexto.VinculosResponsavel.Add(vinculo);
    public void RemoverVinculo(VinculoAlunoResponsavel vinculo) => contexto.VinculosResponsavel.Remove(vinculo);

    public Task<Responsavel?> ObterResponsavelAsync(Guid id, CancellationToken ct = default) =>
        contexto.Responsaveis.FirstOrDefaultAsync(r => r.Responsavel_ID == id, ct);

    public Task<Responsavel?> ResponsavelPorCpfAsync(string cpf, CancellationToken ct = default) =>
        contexto.Responsaveis.FirstOrDefaultAsync(r => r.Cpf == cpf, ct);

    public async Task<IReadOnlyList<ResponsavelEncontrado>> BuscarResponsaveisAsync(string termo, int limite, CancellationToken ct = default)
    {
        var nome = TextoHelper.NormalizarParaBusca(termo);
        var digitos = TextoHelper.SomenteDigitos(termo);
        var consulta = contexto.Responsaveis.AsNoTracking()
            .Where(r => r.NomeBusca.Contains(nome)
                        || (digitos.Length >= 4 && ((r.Cpf != null && r.Cpf.Contains(digitos)) || (r.Telefone != null && r.Telefone.Contains(digitos)))));
        return await Encontrados(consulta.OrderBy(r => r.NomeBusca).Take(limite), ct);
    }

    public async Task<IReadOnlyList<ResponsavelEncontrado>> ResponsaveisParecidosAsync(string? telefone, string nomeBusca, CancellationToken ct = default)
    {
        var consulta = contexto.Responsaveis.AsNoTracking()
            .Where(r => (telefone != null && r.Telefone == telefone) || (nomeBusca != "" && r.NomeBusca == nomeBusca));
        return await Encontrados(consulta.OrderBy(r => r.NomeBusca).Take(10), ct);
    }

    public async Task<Pagina<ResponsavelNaLista>> ListarResponsaveisAsync(string? busca, EscopoTurmas escopo, int pagina, int tamanho,
        CancellationToken ct = default)
    {
        if (!escopo.Todas && escopo.Turmas.Count == 0) return new Pagina<ResponsavelNaLista>([], 0, pagina, tamanho);

        var consulta = contexto.Responsaveis.AsNoTracking();
        // Lista vazia (e não nula) quando é tudo: o EF traduz o Contains sem parâmetro nulo.
        var todas = escopo.Todas;
        var turmasDoEscopo = todas ? [] : escopo.Turmas.ToList();
        if (!todas)
            consulta = consulta.Where(r => contexto.VinculosResponsavel.Any(v => v.Responsavel_ID == r.Responsavel_ID
                && contexto.Matriculas.Any(m => m.Aluno_ID == v.Aluno_ID && m.Situacao == SituacaoMatricula.Ativa && turmasDoEscopo.Contains(m.Turma_ID))));
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = TextoHelper.NormalizarParaBusca(busca);
            var digitos = TextoHelper.SomenteDigitos(busca);
            consulta = consulta.Where(r => r.NomeBusca.Contains(termo) || (digitos.Length >= 4 && r.Telefone != null && r.Telefone.Contains(digitos)));
        }

        var total = await consulta.CountAsync(ct);
        var linhas = await consulta.OrderBy(r => r.NomeBusca).Skip((pagina - 1) * tamanho).Take(tamanho)
            .Select(r => new
            {
                r.Responsavel_ID, r.Nome, r.Telefone, r.Email, r.Pessoa_ID,
                // No escopo do professor, só os filhos das turmas dele (o irmão de outra turma não aparece).
                Alunos = contexto.VinculosResponsavel
                    .Where(v => v.Responsavel_ID == r.Responsavel_ID)
                    .Where(v => todas || contexto.Matriculas.Any(m => m.Aluno_ID == v.Aluno_ID
                        && m.Situacao == SituacaoMatricula.Ativa && turmasDoEscopo.Contains(m.Turma_ID)))
                    .Select(v => v.Aluno!.Nome).ToList()
            })
            .ToListAsync(ct);
        return new Pagina<ResponsavelNaLista>(
            linhas.Select(l => new ResponsavelNaLista(l.Responsavel_ID, l.Nome, l.Telefone, l.Email, l.Pessoa_ID != null,
                l.Alunos.OrderBy(n => n).ToList())).ToList(),
            total, pagina, tamanho);
    }

    public async Task<IReadOnlyList<ResponsavelEncontrado>> TodosResponsaveisAsync(CancellationToken ct = default) =>
        await Encontrados(contexto.Responsaveis.AsNoTracking().OrderBy(r => r.NomeBusca), ct);

    public async Task<IReadOnlyList<ResponsavelEncontrado>> ResponsaveisAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];
        var lista = ids.ToList();
        return await Encontrados(contexto.Responsaveis.AsNoTracking().Where(r => lista.Contains(r.Responsavel_ID)), ct);
    }

    public void AdicionarResponsavel(Responsavel responsavel) => contexto.Responsaveis.Add(responsavel);

    public async Task<IReadOnlyList<Matricula>> MatriculasDoAlunoAsync(Guid alunoId, CancellationToken ct = default) =>
        await contexto.Matriculas.Include(m => m.Turma).Where(m => m.Aluno_ID == alunoId).ToListAsync(ct);

    public Task<Matricula?> ObterMatriculaAsync(Guid id, CancellationToken ct = default) =>
        contexto.Matriculas.Include(m => m.Turma).FirstOrDefaultAsync(m => m.Matricula_ID == id, ct);

    public async Task<IReadOnlyList<AlunoDaTurma>> AlunosDaTurmaAsync(Guid turmaId, CancellationToken ct = default) =>
        await contexto.Matriculas.AsNoTracking()
            .Where(m => m.Turma_ID == turmaId)
            .OrderBy(m => m.Situacao).ThenBy(m => m.Aluno!.NomeBusca)
            .Select(m => new AlunoDaTurma(m.Aluno_ID, m.Aluno!.Nome, m.Aluno.DataNascimento, m.Aluno.Situacao, m.Aluno.TrialAte,
                m.Matricula_ID, m.DataInicio, m.DataFim, m.Situacao))
            .ToListAsync(ct);

    public void AdicionarMatricula(Matricula matricula) => contexto.Matriculas.Add(matricula);

    public async Task<IReadOnlyList<ConsentimentoAluno>> ConsentimentosDoAlunoAsync(Guid alunoId, CancellationToken ct = default) =>
        await contexto.Consentimentos.Include(c => c.Responsavel).Where(c => c.Aluno_ID == alunoId).ToListAsync(ct);

    public Task<ConsentimentoAluno?> ObterConsentimentoAsync(Guid id, CancellationToken ct = default) =>
        contexto.Consentimentos.Include(c => c.Responsavel).FirstOrDefaultAsync(c => c.ConsentimentoAluno_ID == id, ct);

    public void AdicionarConsentimento(ConsentimentoAluno consentimento) => contexto.Consentimentos.Add(consentimento);

    public async Task<IReadOnlyList<PedidoTermos>> PedidosTermosDoAlunoAsync(Guid alunoId, CancellationToken ct = default) =>
        await contexto.PedidosTermos.Include(p => p.Responsavel).Where(p => p.Aluno_ID == alunoId)
            .OrderByDescending(p => p.CriadoEm).ThenBy(p => p.Situacao).ToListAsync(ct); // no mesmo instante, o que ainda vale vem antes

    public Task<PedidoTermos?> ObterPedidoTermosAsync(Guid id, CancellationToken ct = default) =>
        contexto.PedidosTermos.Include(p => p.Responsavel).FirstOrDefaultAsync(p => p.PedidoTermos_ID == id, ct);

    public void AdicionarPedidoTermos(PedidoTermos pedido) => contexto.PedidosTermos.Add(pedido);

    public Task SalvarAsync(CancellationToken ct = default) => Conflitos.SalvarAsync(contexto, ct);

    private async Task<IReadOnlyList<ResponsavelEncontrado>> Encontrados(IQueryable<Responsavel> consulta, CancellationToken ct)
    {
        var linhas = await consulta
            .Select(r => new
            {
                r.Responsavel_ID, r.Nome, r.Cpf, r.Telefone,
                Alunos = contexto.VinculosResponsavel.Where(v => v.Responsavel_ID == r.Responsavel_ID).Select(v => v.Aluno!.Nome).ToList()
            })
            .ToListAsync(ct);
        return linhas.Select(l => new ResponsavelEncontrado(l.Responsavel_ID, l.Nome, l.Cpf, l.Telefone, l.Alunos.OrderBy(n => n).ToList())).ToList();
    }
}

/// <summary>As perguntas de escopo do motor de permissões (professor ↔ turma, aluno e sessão), na escolinha do contexto.</summary>
public class ConsultasEscopo(EscolinhaDbContext contexto) : IConsultasEscopo
{
    public Task<bool> ProfessorDaTurmaAsync(Guid pessoaId, Guid turmaId, CancellationToken ct = default) =>
        contexto.Turmas.AnyAsync(t => t.Turma_ID == turmaId && t.Professor!.Pessoa_ID == pessoaId, ct);

    public Task<bool> ProfessorDoAlunoAsync(Guid pessoaId, Guid alunoId, CancellationToken ct = default) =>
        contexto.Matriculas.AnyAsync(m => m.Aluno_ID == alunoId && m.Situacao == SituacaoMatricula.Ativa
                                          && m.Turma!.Professor!.Pessoa_ID == pessoaId, ct);

    public Task<bool> ProfessorDaSessaoAsync(Guid pessoaId, Guid sessaoId, CancellationToken ct = default) =>
        contexto.Sessoes.AnyAsync(s => s.SessaoTreino_ID == sessaoId
                                       && (s.Professor!.Pessoa_ID == pessoaId || s.Turma!.Professor!.Pessoa_ID == pessoaId), ct);

    public Task<bool> ProprioProfessorAsync(Guid pessoaId, Guid professorId, CancellationToken ct = default) =>
        contexto.Professores.AnyAsync(p => p.Professor_ID == professorId && p.Pessoa_ID == pessoaId, ct);

    public Task<bool> ProfessorDoEventoAsync(Guid pessoaId, Guid eventoId, CancellationToken ct = default) =>
        contexto.EventosTurma.AnyAsync(et => et.Evento_ID == eventoId
                                             && contexto.Turmas.Any(t => t.Turma_ID == et.Turma_ID && t.Professor!.Pessoa_ID == pessoaId), ct);

    public async Task<IReadOnlyList<Guid>> TurmasDoProfessorAsync(Guid pessoaId, CancellationToken ct = default) =>
        await contexto.Turmas.Where(t => t.Professor!.Pessoa_ID == pessoaId).Select(t => t.Turma_ID).ToListAsync(ct);

    public Task<bool> ResponsavelVerificadoDoAlunoAsync(Guid pessoaId, Guid alunoId, CancellationToken ct = default) =>
        contexto.VinculosResponsavel.AnyAsync(v => v.Aluno_ID == alunoId && v.Responsavel!.Pessoa_ID == pessoaId
                                                   && v.Verificacao == VerificacaoVinculo.Verificado, ct);

    public Task<bool> ProprioResponsavelAsync(Guid pessoaId, Guid responsavelId, CancellationToken ct = default) =>
        contexto.Responsaveis.AnyAsync(r => r.Responsavel_ID == responsavelId && r.Pessoa_ID == pessoaId, ct);
}
