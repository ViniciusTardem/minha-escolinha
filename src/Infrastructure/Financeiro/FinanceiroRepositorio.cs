using Microsoft.EntityFrameworkCore;
using MinhaEscolinha.Application.Financeiro;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Financeiro;
using MinhaEscolinha.Infrastructure.Cadastro;
using MinhaEscolinha.Infrastructure.Data.Contexto;

namespace MinhaEscolinha.Infrastructure.Financeiro;

public class FinanceiroRepositorio(EscolinhaDbContext contexto) : IFinanceiroRepositorio
{
    public async Task<IReadOnlyList<MatriculaParaCobranca>> MatriculasParaCobrancaAsync(DateOnly encerradasDesde, CancellationToken ct = default) =>
        await contexto.Matriculas.AsNoTracking()
            .Where(m => m.Situacao == SituacaoMatricula.Ativa || (m.DataFim != null && m.DataFim >= encerradasDesde))
            .Select(m => new MatriculaParaCobranca(m.Matricula_ID, m.Aluno_ID, m.Turma_ID, m.DataInicio, m.DataFim, m.Mensalidade,
                m.Aluno!.Situacao == SituacaoAluno.Ativo,
                contexto.Turmas.Where(t => t.Turma_ID == m.Turma_ID).Select(t => t.PeriodicidadeMeses).FirstOrDefault()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Cobranca>> CobrancasDesdeAsync(DateOnly competencia, CancellationToken ct = default) =>
        await contexto.Cobrancas.Where(c => c.Competencia >= competencia).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> MatriculasComCobrancaAsync(IReadOnlyCollection<Guid> matriculas, CancellationToken ct = default)
    {
        if (matriculas.Count == 0) return [];
        var ids = matriculas.ToList();
        return await contexto.Cobrancas.AsNoTracking().Where(c => ids.Contains(c.Matricula_ID)).Select(c => c.Matricula_ID).Distinct().ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Cobranca>> PrevistasAteAsync(DateOnly vencimento, CancellationToken ct = default) =>
        await contexto.Cobrancas.Where(c => c.Situacao == SituacaoCobranca.Prevista && c.Vencimento <= vencimento).ToListAsync(ct);

    public async Task<IReadOnlyList<Cobranca>> RecusadasAsync(CancellationToken ct = default) =>
        await contexto.Cobrancas.Where(c => c.Situacao == SituacaoCobranca.Prevista && c.ErroEmissaoDefinitivo).ToListAsync(ct);

    public async Task<IReadOnlyList<Cobranca>> EmAbertoAsync(CancellationToken ct = default) =>
        await contexto.Cobrancas.Where(c => c.Situacao == SituacaoCobranca.Emitida || c.Situacao == SituacaoCobranca.Vencida).ToListAsync(ct);

    public async Task<IReadOnlyList<Cobranca>> ParaConferirAsync(DateTime conferidasAntesDe, int limite, CancellationToken ct = default) =>
        await contexto.Cobrancas
            .Where(c => (c.Situacao == SituacaoCobranca.Emitida || c.Situacao == SituacaoCobranca.Vencida) && c.IdAsaas != null
                        && (c.ConferidaEm == null || c.ConferidaEm < conferidasAntesDe))
            .OrderBy(c => c.ConferidaEm)
            .Take(limite)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Cobranca>> PrevistasDasMatriculasAposAsync(IReadOnlyCollection<Guid> matriculas, DateOnly data,
        CancellationToken ct = default)
    {
        var ids = matriculas.ToList();
        return await contexto.Cobrancas
            .Where(c => c.Situacao == SituacaoCobranca.Prevista && ids.Contains(c.Matricula_ID) && c.Vencimento > data)
            .ToListAsync(ct);
    }

    public Task<Cobranca?> ObterAsync(Guid id, CancellationToken ct = default) =>
        contexto.Cobrancas.FirstOrDefaultAsync(c => c.Cobranca_ID == id, ct);

    public Task<Cobranca?> ObterPorIdAsaasAsync(string idAsaas, CancellationToken ct = default) =>
        contexto.Cobrancas.FirstOrDefaultAsync(c => c.IdAsaas == idAsaas, ct);

    public Task<PagadorDoAluno?> PagadorAsync(Guid alunoId, CancellationToken ct = default) =>
        contexto.VinculosResponsavel.AsNoTracking()
            .Where(v => v.Aluno_ID == alunoId && v.Pagador)
            .Select(v => new PagadorDoAluno(v.Responsavel_ID, v.Responsavel!.Nome, v.Responsavel.Cpf, v.Responsavel.Email, v.Responsavel.Telefone))
            .FirstOrDefaultAsync(ct);

    public async Task<string> NomeDoAlunoAsync(Guid alunoId, CancellationToken ct = default) =>
        await contexto.Alunos.AsNoTracking().Where(a => a.Aluno_ID == alunoId).Select(a => a.Nome).FirstOrDefaultAsync(ct) ?? "—";

    public Task<ClienteAsaas?> ClienteAsaasAsync(Guid responsavelId, Guid conta, CancellationToken ct = default) =>
        contexto.ClientesAsaas.AsNoTracking().FirstOrDefaultAsync(c => c.Responsavel_ID == responsavelId && c.Conta == conta, ct);

    public void AdicionarCliente(ClienteAsaas cliente) => contexto.ClientesAsaas.Add(cliente);

    public async Task<IReadOnlyList<LinhaMensalidade>> ListarAsync(FiltroMensalidades filtro, CancellationToken ct = default)
    {
        var consulta = contexto.Cobrancas.AsNoTracking();
        if (filtro.SoDivergencias) consulta = consulta.Where(c => c.Sinal != SinalCobranca.Nenhum);
        else if (filtro.Competencia is { } mes)
        {
            var competencia = new DateOnly(mes.Year, mes.Month, 1);
            consulta = consulta.Where(c => c.Competencia == competencia);
        }
        if (filtro.TurmaId is { } turma) consulta = consulta.Where(c => c.Turma_ID == turma);
        if (TextoHelper.NormalizarParaBusca(filtro.Busca) is { Length: > 0 } termo)
            consulta = consulta.Where(c => contexto.Alunos.Any(a => a.Aluno_ID == c.Aluno_ID && a.NomeBusca.Contains(termo)));

        var linhas = await ProjetarAsync(consulta, ct);
        return linhas
            .Where(l => filtro.Grupo is null || l.Grupo == filtro.Grupo)
            .OrderBy(l => l.Grupo).ThenBy(l => l.Vencimento).ThenBy(l => l.Aluno, StringComparer.Create(new System.Globalization.CultureInfo("pt-BR"), true))
            .ToList();
    }

    public async Task<IReadOnlyList<LinhaMensalidade>> DoAlunoAsync(Guid alunoId, int limite, CancellationToken ct = default) =>
        (await ProjetarAsync(contexto.Cobrancas.AsNoTracking().Where(c => c.Aluno_ID == alunoId)
                .OrderByDescending(c => c.Competencia).Take(limite), ct))
            .OrderByDescending(l => l.Competencia).ThenBy(l => l.Turma)
            .ToList();

    public async Task<LinhaMensalidade?> LinhaAsync(Guid id, CancellationToken ct = default) =>
        (await ProjetarAsync(contexto.Cobrancas.AsNoTracking().Where(c => c.Cobranca_ID == id), ct)).FirstOrDefault();

    public Task<int> ContarDivergenciasAsync(CancellationToken ct = default) =>
        contexto.Cobrancas.CountAsync(c => c.Sinal != SinalCobranca.Nenhum, ct);

    public async Task<IReadOnlyList<OpcaoTurmaFinanceiro>> TurmasAsync(CancellationToken ct = default) =>
        await contexto.Turmas.AsNoTracking().OrderBy(t => t.Nome).Select(t => new OpcaoTurmaFinanceiro(t.Turma_ID, t.Nome, t.PeriodicidadeMeses)).ToListAsync(ct);

    public async Task<ResumoFinanceiro> ResumoAsync(DateOnly emitirAte, CancellationToken ct = default)
    {
        var aConfirmar = await contexto.Cobrancas.CountAsync(c => c.Situacao == SituacaoCobranca.Prevista && c.PrimeiraDaMatricula
                                                                   && c.ConfirmadaEm == null, ct);
        // Agrupado por constante: um grupo só, ou nenhum (sem vencidas).
        var vencidas = await contexto.Cobrancas.Where(c => c.Situacao == SituacaoCobranca.Vencida)
            .GroupBy(_ => 1).Select(g => new { Quantidade = g.Count(), Valor = g.Sum(c => c.Valor) }).SingleOrDefaultAsync(ct);
        var divergencias = await ContarDivergenciasAsync(ct);
        var impedidas = await contexto.Cobrancas.CountAsync(c => c.Situacao == SituacaoCobranca.Prevista && c.Vencimento <= emitirAte
            && !(c.PrimeiraDaMatricula && c.ConfirmadaEm == null)
            && (c.ErroEmissaoDefinitivo
                || !contexto.VinculosResponsavel.Any(v => v.Aluno_ID == c.Aluno_ID && v.Pagador && v.Responsavel!.Cpf != null)), ct);
        var conta = await contexto.ContasAsaas.AsNoTracking().AnyAsync(ct);
        return new ResumoFinanceiro(aConfirmar, vencidas?.Quantidade ?? 0, vencidas?.Valor ?? 0m, divergencias, impedidas, conta);
    }

    public void Adicionar(Cobranca cobranca) => contexto.Cobrancas.Add(cobranca);
    public void Remover(Cobranca cobranca) => contexto.Cobrancas.Remove(cobranca);

    public Task SalvarAsync(CancellationToken ct = default) => Conflitos.SalvarAsync(contexto, ct);

    /// <summary>As linhas com os nomes, o pagador de hoje (para a prevista) e o que impede a emissão.</summary>
    private async Task<List<LinhaMensalidade>> ProjetarAsync(IQueryable<Cobranca> consulta, CancellationToken ct)
    {
        var dados = await (
            from c in consulta
            join a in contexto.Alunos.AsNoTracking() on c.Aluno_ID equals a.Aluno_ID
            join t in contexto.Turmas.AsNoTracking() on c.Turma_ID equals t.Turma_ID
            select new
            {
                Cobranca = c,
                Aluno = a.Nome,
                AlunoSituacao = a.Situacao,
                Turma = t.Nome,
                Pagador = contexto.VinculosResponsavel
                    .Where(v => v.Aluno_ID == c.Aluno_ID && v.Pagador)
                    .Select(v => new { v.Responsavel!.Nome, v.Responsavel.Cpf })
                    .FirstOrDefault()
            }).ToListAsync(ct);
        var contaVinculada = await contexto.ContasAsaas.AsNoTracking().AnyAsync(ct);

        return dados.Select(d =>
        {
            var c = d.Cobranca;
            string? impedimento = null;
            if (c.Situacao == SituacaoCobranca.Prevista && !c.AguardandoConfirmacao)
                impedimento = !contaVinculada ? "Sem conta Asaas vinculada"
                    : d.Pagador is null ? "Sem pagador marcado"
                    : !Cpf.EhValido(Cpf.Normalizar(d.Pagador.Cpf)) ? "Pagador sem CPF válido"
                    : c.ErroEmissaoDefinitivo ? $"Recusada pela Asaas: {c.ErroEmissao}"
                    : null;
            return new LinhaMensalidade(c.Cobranca_ID, c.Aluno_ID, d.Aluno, d.Turma, c.Competencia, c.Vencimento, c.Valor, c.Situacao,
                c.AguardandoConfirmacao, c.Sinal, c.SinalDetalhe, c.PagadorNome ?? d.Pagador?.Nome, c.UrlFatura, c.PagaEm, c.ValorPago, c.Meio,
                c.ErroEmissao, c.UltimaEtapa, c.UltimaEtapaData, c.Motivo, d.AlunoSituacao == SituacaoAluno.Desistente, impedimento,
                c.Situacao == SituacaoCobranca.Prevista && c.ErroEmissaoDefinitivo);
        }).ToList();
    }
}
