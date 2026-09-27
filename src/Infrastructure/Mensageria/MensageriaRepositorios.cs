using Microsoft.EntityFrameworkCore;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Equipe;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Domain.Turmas;
using MinhaEscolinha.Infrastructure.Cadastro;
using MinhaEscolinha.Infrastructure.Data.Contexto;

namespace MinhaEscolinha.Infrastructure.Mensageria;

/// <summary>Mensagens da escolinha do contexto (filtro de escolinha); templates e rastreio do WhatsApp são globais.</summary>
public class MensageriaRepositorio(EscolinhaDbContext contexto) : IMensageriaRepositorio
{
    public void AdicionarMensagem(Mensagem mensagem) => contexto.Mensagens.Add(mensagem);

    public Task<bool> ChaveEmUsoAsync(string chave, CancellationToken ct = default) =>
        contexto.Mensagens.AnyAsync(m => m.Chave == chave, ct);

    public async Task<IReadOnlyList<Mensagem>> CancelaveisDaOrigemAsync(string tipoOrigem, Guid origemId, CancellationToken ct = default) =>
        await contexto.Mensagens
            .Where(m => m.TipoOrigem == tipoOrigem && m.Origem_ID == origemId
                        && ((m.Canal == CanalMensagem.WhatsApp && (m.Situacao == SituacaoMensagem.Pendente || m.Situacao == SituacaoMensagem.Retida))
                            || (m.Canal == CanalMensagem.Portal && m.Situacao == SituacaoMensagem.Enviada && m.LidaEm == null)))
            .ToListAsync(ct);

    public Task<bool> AlgumaSaiuDaOrigemAsync(string tipoOrigem, Guid origemId, CancellationToken ct = default) =>
        contexto.Mensagens.AnyAsync(m => m.TipoOrigem == tipoOrigem && m.Origem_ID == origemId
                                         && ((m.Canal == CanalMensagem.WhatsApp && (m.Situacao == SituacaoMensagem.Enviada
                                                  || m.Situacao == SituacaoMensagem.Entregue || m.Situacao == SituacaoMensagem.Lida))
                                             || (m.Canal == CanalMensagem.Portal && m.LidaEm != null)), ct);

    public async Task<IReadOnlyList<Mensagem>> MensagensDasOrigensAsync(string tipoOrigem, IReadOnlyCollection<Guid> origens,
        CancellationToken ct = default) =>
        origens.Count == 0
            ? []
            : await contexto.Mensagens.AsNoTracking()
                .Where(m => m.TipoOrigem == tipoOrigem && origens.Contains(m.Origem_ID))
                .OrderBy(m => m.DestinatarioNome).ThenBy(m => m.Canal)
                .ToListAsync(ct);

    public Task<Mensagem?> ObterMensagemAsync(Guid id, CancellationToken ct = default) =>
        contexto.Mensagens.Include(m => m.Respostas).FirstOrDefaultAsync(m => m.Mensagem_ID == id, ct);

    public Task<Mensagem?> DetalharMensagemAsync(Guid id, CancellationToken ct = default) =>
        contexto.Mensagens.AsNoTracking().Include(m => m.Eventos).Include(m => m.Respostas).AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Mensagem_ID == id, ct);

    public async Task<IReadOnlyList<Guid>> ProntasParaEnviarAsync(DateTime agora, int limite, CancellationToken ct = default) =>
        await contexto.Mensagens.AsNoTracking()
            .Where(m => m.Canal == CanalMensagem.WhatsApp && m.Situacao == SituacaoMensagem.Pendente
                        && (m.AgendadaPara == null || m.AgendadaPara <= agora)
                        && (m.ReservadaAte == null || m.ReservadaAte < agora))
            .OrderBy(m => m.CriadaEm).Take(limite).Select(m => m.Mensagem_ID).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> RetidasAsync(int limite, CancellationToken ct = default) =>
        await contexto.Mensagens.AsNoTracking()
            .Where(m => m.Canal == CanalMensagem.WhatsApp && m.Situacao == SituacaoMensagem.Retida)
            .OrderBy(m => m.CriadaEm).Take(limite).Select(m => m.Mensagem_ID).ToListAsync(ct);

    public Task<int> ContarRetidasAsync(CancellationToken ct = default) =>
        contexto.Mensagens.CountAsync(m => m.Situacao == SituacaoMensagem.Retida, ct);

    public async Task<Pagina<Mensagem>> ListarMensagensAsync(FiltroMensagens filtro, int pagina, int tamanho, CancellationToken ct = default)
    {
        var consulta = contexto.Mensagens.AsNoTracking();
        if (filtro.De is { } de) consulta = consulta.Where(m => m.CriadaEm >= de.ToDateTime(TimeOnly.MinValue));
        if (filtro.Ate is { } ate) consulta = consulta.Where(m => m.CriadaEm < ate.AddDays(1).ToDateTime(TimeOnly.MinValue));
        if (filtro.Gatilho is { } gatilho) consulta = consulta.Where(m => m.Gatilho == gatilho);
        if (filtro.Canal is { } canal) consulta = consulta.Where(m => m.Canal == canal);
        if (filtro.Situacao is { } situacao) consulta = consulta.Where(m => m.Situacao == situacao);
        if (filtro.AlunoId is { } alunoId) consulta = consulta.Where(m => m.Aluno_ID == alunoId);
        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            var termo = filtro.Busca.Trim();
            consulta = consulta.Where(m => m.DestinatarioNome.Contains(termo) || m.Conteudo.Contains(termo));
        }

        var total = await consulta.CountAsync(ct);
        var itens = await consulta.OrderByDescending(m => m.CriadaEm).ThenBy(m => m.Mensagem_ID)
            .Skip((pagina - 1) * tamanho).Take(tamanho).ToListAsync(ct);
        return new Pagina<Mensagem>(itens, total, pagina, tamanho);
    }

    public async Task<IReadOnlyDictionary<SituacaoMensagem, int>> ContarPorSituacaoAsync(DateTime desde, CancellationToken ct = default) =>
        await contexto.Mensagens.AsNoTracking().Where(m => m.CriadaEm >= desde)
            .GroupBy(m => m.Situacao).Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);

    public async Task<IReadOnlyList<Mensagem>> AvisosDoPortalAsync(Guid pessoaId, int limite, CancellationToken ct = default) =>
        await contexto.Mensagens
            .Where(m => m.Canal == CanalMensagem.Portal && m.Pessoa_ID == pessoaId && m.Situacao != SituacaoMensagem.Suprimida)
            .OrderByDescending(m => m.CriadaEm).Take(limite).ToListAsync(ct);

    public Task<int> AvisosNaoLidosAsync(Guid pessoaId, CancellationToken ct = default) =>
        contexto.Mensagens.CountAsync(m => m.Canal == CanalMensagem.Portal && m.Pessoa_ID == pessoaId && m.LidaEm == null
                                           && m.Situacao != SituacaoMensagem.Suprimida, ct);

    public async Task<IReadOnlyList<DestinatarioResponsavel>> ResponsaveisDaTurmaAsync(Guid turmaId, CancellationToken ct = default)
    {
        var linhas = await (
            from m in contexto.Matriculas.AsNoTracking()
            where m.Turma_ID == turmaId && m.Situacao == SituacaoMatricula.Ativa
                  && (m.Aluno!.Situacao == SituacaoAluno.Trial || m.Aluno.Situacao == SituacaoAluno.Ativo)
            join v in contexto.VinculosResponsavel.AsNoTracking() on m.Aluno_ID equals v.Aluno_ID
            where v.RecebeNotificacao
            select new
            {
                v.Responsavel_ID,
                v.Responsavel!.Nome,
                v.Responsavel.Telefone,
                v.Responsavel.Pessoa_ID,
                v.Verificacao,
                AlunoId = m.Aluno_ID,
                AlunoNome = m.Aluno!.Nome
            }).ToListAsync(ct);

        var pessoas = linhas.Where(l => l.Pessoa_ID is not null).Select(l => l.Pessoa_ID!.Value).Distinct().ToList();
        var comAcesso = (await contexto.VinculosAtuacao.AsNoTracking()
                .Where(v => pessoas.Contains(v.Pessoa_ID) && v.Papel == PapelAtuacao.Responsavel && v.Situacao == SituacaoVinculo.Ativo)
                .Select(v => v.Pessoa_ID).ToListAsync(ct))
            .ToHashSet();

        return linhas.GroupBy(l => l.Responsavel_ID)
            .Select(g =>
            {
                var r = g.First();
                return new DestinatarioResponsavel(r.Responsavel_ID, r.Nome, r.Telefone, r.Pessoa_ID,
                    r.Pessoa_ID is { } pessoa && comAcesso.Contains(pessoa),
                    g.All(l => l.Verificacao == VerificacaoVinculo.Verificado),
                    g.DistinctBy(l => l.AlunoId).OrderBy(l => l.AlunoNome).Select(l => (l.AlunoId, l.AlunoNome)).ToList());
            })
            .OrderBy(d => d.Nome)
            .ToList();
    }

    public async Task<IReadOnlyList<DestinatarioResponsavel>> ResponsaveisDoAlunoAsync(Guid alunoId, CancellationToken ct = default)
    {
        var linhas = await contexto.VinculosResponsavel.AsNoTracking()
            .Where(v => v.Aluno_ID == alunoId && v.RecebeNotificacao)
            .Select(v => new
            {
                v.Responsavel_ID,
                v.Responsavel!.Nome,
                v.Responsavel.Telefone,
                v.Responsavel.Pessoa_ID,
                v.Verificacao,
                AlunoNome = v.Aluno!.Nome
            }).ToListAsync(ct);

        var pessoas = linhas.Where(l => l.Pessoa_ID is not null).Select(l => l.Pessoa_ID!.Value).Distinct().ToList();
        var comAcesso = (await contexto.VinculosAtuacao.AsNoTracking()
                .Where(v => pessoas.Contains(v.Pessoa_ID) && v.Papel == PapelAtuacao.Responsavel && v.Situacao == SituacaoVinculo.Ativo)
                .Select(v => v.Pessoa_ID).ToListAsync(ct))
            .ToHashSet();

        return linhas
            .Select(l => new DestinatarioResponsavel(l.Responsavel_ID, l.Nome, l.Telefone, l.Pessoa_ID,
                l.Pessoa_ID is { } pessoa && comAcesso.Contains(pessoa), l.Verificacao == VerificacaoVinculo.Verificado,
                [(alunoId, l.AlunoNome)]))
            .OrderBy(d => d.Nome)
            .ToList();
    }

    public async Task<IReadOnlyList<TemplateMensagem>> ListarTemplatesAsync(CancellationToken ct = default) =>
        await contexto.TemplatesMensagem.AsNoTracking().ToListAsync(ct);

    public Task<TemplateMensagem?> ObterTemplateAsync(Guid id, CancellationToken ct = default) =>
        contexto.TemplatesMensagem.FirstOrDefaultAsync(t => t.TemplateMensagem_ID == id, ct);

    public Task<TemplateMensagem?> TemplateDoGatilhoAsync(GatilhoMensagem gatilho, TomMensagem tom, CancellationToken ct = default) =>
        contexto.TemplatesMensagem.AsNoTracking().FirstOrDefaultAsync(t => t.Gatilho == gatilho && t.Tom == tom, ct);

    public Task<TemplateMensagem?> TemplatePorNomeAsync(string nomeNaMeta, CancellationToken ct = default) =>
        contexto.TemplatesMensagem.AsNoTracking().FirstOrDefaultAsync(t => t.NomeNaMeta == nomeNaMeta, ct);

    public void AdicionarTemplate(TemplateMensagem template) => contexto.TemplatesMensagem.Add(template);

    public void AdicionarRastreio(RastreioWhatsApp rastreio) => contexto.RastreiosWhatsApp.Add(rastreio);

    public Task<RastreioWhatsApp?> RastreioPorIdExternoAsync(string idExterno, CancellationToken ct = default) =>
        contexto.RastreiosWhatsApp.AsNoTracking().FirstOrDefaultAsync(r => r.IdExterno == idExterno, ct);

    public Task<RastreioWhatsApp?> UltimoRastreioDoTelefoneAsync(string chaveTelefone, CancellationToken ct = default) =>
        contexto.RastreiosWhatsApp.AsNoTracking().Where(r => r.ChaveTelefone == chaveTelefone)
            .OrderByDescending(r => r.EnviadoEm).FirstOrDefaultAsync(ct);

    public Task SalvarAsync(CancellationToken ct = default) => Conflitos.SalvarAsync(contexto, ct);
}

/// <summary>Convites, fichas convidáveis e vínculos a confirmar, na escolinha do contexto.</summary>
public class ConvitesRepositorio(EscolinhaDbContext contexto) : IConvitesRepositorio
{
    public void AdicionarConvite(ConviteAcesso convite) => contexto.Convites.Add(convite);

    public Task<ConviteAcesso?> ObterConviteAsync(Guid id, CancellationToken ct = default) =>
        contexto.Convites.FirstOrDefaultAsync(c => c.ConviteAcesso_ID == id, ct);

    public async Task<IReadOnlyList<ConviteAcesso>> PendentesDaFichaAsync(TipoConvite tipo, Guid fichaId, CancellationToken ct = default) =>
        await contexto.Convites
            .Where(c => c.Tipo == tipo && c.Situacao == SituacaoConvite.Pendente
                        && (tipo == TipoConvite.Responsavel ? c.Responsavel_ID == fichaId : c.Professor_ID == fichaId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CandidatoAcesso>> CandidatosAsync(DateTime agora, CancellationToken ct = default)
    {
        var responsaveis = await contexto.Responsaveis.AsNoTracking()
            .Select(r => new { r.Responsavel_ID, r.Nome, r.Telefone, r.Pessoa_ID }).ToListAsync(ct);
        var filhos = (await contexto.VinculosResponsavel.AsNoTracking()
                .Where(v => v.Aluno!.Situacao != SituacaoAluno.Desistente)
                .Select(v => new { v.Responsavel_ID, v.Aluno!.Nome }).ToListAsync(ct))
            .ToLookup(v => v.Responsavel_ID, v => v.Nome);

        var professores = await contexto.Professores.AsNoTracking()
            .Select(p => new { p.Professor_ID, p.Nome, p.Telefone, p.Pessoa_ID }).ToListAsync(ct);
        var turmas = (await contexto.Turmas.AsNoTracking().Where(t => t.Situacao == SituacaoTurma.Ativa)
                .Select(t => new { t.Professor_ID, t.Nome }).ToListAsync(ct))
            .ToLookup(t => t.Professor_ID, t => t.Nome);

        var ativos = (await contexto.VinculosAtuacao.AsNoTracking().Where(v => v.Situacao == SituacaoVinculo.Ativo)
                .Select(v => new { v.Pessoa_ID, v.Papel }).ToListAsync(ct))
            .Select(v => (v.Pessoa_ID, v.Papel)).ToHashSet();
        var convites = (await contexto.Convites.AsNoTracking()
                .Select(c => new { c.ConviteAcesso_ID, c.Tipo, c.Responsavel_ID, c.Professor_ID, c.Situacao, c.EmitidoEm, c.ExpiraEm })
                .ToListAsync(ct))
            .GroupBy(c => (c.Tipo, Ficha: c.Tipo == TipoConvite.Responsavel ? c.Responsavel_ID : c.Professor_ID))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.EmitidoEm).First());

        CandidatoAcesso Montar(TipoConvite tipo, Guid id, string nome, string? telefone, Guid? pessoa, string? detalhe)
        {
            var papel = tipo == TipoConvite.Responsavel ? PapelAtuacao.Responsavel : PapelAtuacao.Professor;
            convites.TryGetValue((tipo, id), out var ultimo);
            var situacao = pessoa is { } p && ativos.Contains((p, papel)) ? SituacaoAcesso.Ativo
                : ultimo is { Situacao: SituacaoConvite.Pendente } && ultimo.ExpiraEm > agora ? SituacaoAcesso.Convidado
                : ultimo is { Situacao: SituacaoConvite.Pendente } ? SituacaoAcesso.ConviteExpirado
                : SituacaoAcesso.SemAcesso;
            return new CandidatoAcesso(tipo, id, nome, telefone, detalhe, situacao, ultimo?.ConviteAcesso_ID, ultimo?.EmitidoEm, ultimo?.ExpiraEm);
        }

        return responsaveis
            .Select(r => Montar(TipoConvite.Responsavel, r.Responsavel_ID, r.Nome, r.Telefone, r.Pessoa_ID,
                filhos[r.Responsavel_ID].Any() ? string.Join(", ", filhos[r.Responsavel_ID].Order()) : null))
            .Concat(professores.Select(p => Montar(TipoConvite.Professor, p.Professor_ID, p.Nome, p.Telefone, p.Pessoa_ID,
                turmas[p.Professor_ID].Any() ? string.Join(", ", turmas[p.Professor_ID].Order()) : null)))
            .OrderBy(c => c.Nome)
            .ToList();
    }

    public async Task<IReadOnlyList<VinculoParaVerificar>> VinculosParaVerificarAsync(CancellationToken ct = default) =>
        await contexto.VinculosResponsavel.AsNoTracking()
            .Where(v => v.Verificacao == VerificacaoVinculo.Pendente)
            .OrderBy(v => v.VerificacaoSolicitadaEm).ThenBy(v => v.Responsavel!.Nome)
            .Select(v => new VinculoParaVerificar(v.VinculoAlunoResponsavel_ID, v.Responsavel_ID, v.Responsavel!.Nome, v.Responsavel.Cpf,
                v.Responsavel.Telefone, v.Aluno_ID, v.Aluno!.Nome, v.Parentesco, v.VerificacaoSolicitadaEm))
            .ToListAsync(ct);

    public Task<int> ContarVinculosParaVerificarAsync(CancellationToken ct = default) =>
        contexto.VinculosResponsavel.CountAsync(v => v.Verificacao == VerificacaoVinculo.Pendente, ct);

    public Task<VinculoAlunoResponsavel?> ObterVinculoAsync(Guid id, CancellationToken ct = default) =>
        contexto.VinculosResponsavel.Include(v => v.Aluno).Include(v => v.Responsavel)
            .FirstOrDefaultAsync(v => v.VinculoAlunoResponsavel_ID == id, ct);

    public async Task<IReadOnlyList<VinculoAlunoResponsavel>> VinculosDoResponsavelAsync(Guid responsavelId, CancellationToken ct = default) =>
        await contexto.VinculosResponsavel.Where(v => v.Responsavel_ID == responsavelId).ToListAsync(ct);

    public Task<Responsavel?> ObterResponsavelAsync(Guid id, CancellationToken ct = default) =>
        contexto.Responsaveis.FirstOrDefaultAsync(r => r.Responsavel_ID == id, ct);

    public Task<Professor?> ObterProfessorAsync(Guid id, CancellationToken ct = default) =>
        contexto.Professores.FirstOrDefaultAsync(p => p.Professor_ID == id, ct);

    public Task<bool> CpfDeOutraFichaAsync(TipoConvite tipo, string cpf, Guid fichaId, CancellationToken ct = default) =>
        tipo == TipoConvite.Responsavel
            ? contexto.Responsaveis.AnyAsync(r => r.Cpf == cpf && r.Responsavel_ID != fichaId, ct)
            : contexto.Professores.AnyAsync(p => p.Cpf == cpf && p.Professor_ID != fichaId, ct);

    public async Task<IReadOnlyList<FilhoDaPessoa>> FilhosDaPessoaAsync(Guid pessoaId, CancellationToken ct = default)
    {
        var vinculos = await contexto.VinculosResponsavel.AsNoTracking()
            .Where(v => v.Responsavel!.Pessoa_ID == pessoaId)
            .Select(v => new { v.Aluno_ID, v.Aluno!.Nome, v.Aluno.Situacao, v.Verificacao })
            .ToListAsync(ct);
        var alunos = vinculos.Select(v => v.Aluno_ID).Distinct().ToList();
        var turmas = (await contexto.Matriculas.AsNoTracking()
                .Where(m => alunos.Contains(m.Aluno_ID) && m.Situacao == SituacaoMatricula.Ativa)
                .Select(m => new { m.Aluno_ID, m.Turma!.Nome }).ToListAsync(ct))
            .ToLookup(m => m.Aluno_ID, m => m.Nome);

        return vinculos
            .GroupBy(v => v.Aluno_ID)
            .Select(g =>
            {
                var v = g.First();
                // Mais de uma ficha da mesma pessoa (raro): vale o vínculo mais adiantado.
                var verificacao = g.Max(x => x.Verificacao);
                return new FilhoDaPessoa(v.Aluno_ID, v.Nome, v.Situacao, turmas[v.Aluno_ID].Order().ToList(), verificacao);
            })
            .OrderBy(f => f.Nome)
            .ToList();
    }

    public Task<bool> AcessoAtivoAsync(Guid pessoaId, PapelAtuacao papel, CancellationToken ct = default) =>
        contexto.VinculosAtuacao.AnyAsync(v => v.Pessoa_ID == pessoaId && v.Papel == papel && v.Situacao == SituacaoVinculo.Ativo, ct);

    public Task SalvarAsync(CancellationToken ct = default) => Conflitos.SalvarAsync(contexto, ct);
}
