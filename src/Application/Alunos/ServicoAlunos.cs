using System.Globalization;
using FluentValidation;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Application.Alunos;

/// <summary>
/// Quanto do dado de saúde cada pessoa vê (R5, BL-M03-10): o gestor e o responsável do próprio aluno, tudo; o professor
/// da turma em que o aluno treina, o necessário ao treino (P20); os demais, nada — o campo nem sai do banco para eles.
/// </summary>
public sealed class PoliticaSaude(IAutorizacao autorizacao, IContextoEscolinha contexto, IResolvedorEscopo resolvedor, IUsuarioAtual usuarioAtual)
{
    public async Task<AcessoSaude> AcessoAsync(Guid alunoId, CancellationToken ct = default)
    {
        if (autorizacao.Nivel(Recurso.DocumentosObservacoesMedicas) == NivelAcesso.Total) return AcessoSaude.Completo;
        if (usuarioAtual.Id is not { } pessoaId) return AcessoSaude.Nenhum;

        var alvo = new AlvoEscopo(TipoAlvoEscopo.Aluno, alunoId);
        if (contexto.Papeis.Contains(PapelAtuacao.Responsavel) && await resolvedor.NoEscopoAsync(PapelAtuacao.Responsavel, alvo, pessoaId, ct))
            return AcessoSaude.Completo;
        if (contexto.Papeis.Contains(PapelAtuacao.Professor) && await resolvedor.NoEscopoAsync(PapelAtuacao.Professor, alvo, pessoaId, ct))
            return AcessoSaude.Treino;
        return AcessoSaude.Nenhum;
    }

    public static SaudeDaFicha? Filtrar(Aluno a, AcessoSaude acesso) => acesso switch
    {
        AcessoSaude.Completo => new SaudeDaFicha(a.Alergias, a.Restricoes, a.ObservacoesMedicas,
            a.ContatoEmergenciaNome, a.ContatoEmergenciaTelefone, a.ContatoEmergenciaParentesco),
        AcessoSaude.Treino => new SaudeDaFicha(a.Alergias, a.Restricoes, null,
            a.ContatoEmergenciaNome, a.ContatoEmergenciaTelefone, a.ContatoEmergenciaParentesco),
        _ => null
    };
}

/// <summary>
/// Cadastro de aluno (BL-M03-02) com os responsáveis sem duplicidade (BL-M03-01, CA-1), a matrícula (BL-M03-04), a saúde
/// (BL-M03-11) e os consentimentos (BL-M03-09) gravados juntos; as duas visões da base (BL-M03-05); a ficha com o dado
/// sensível filtrado (BL-M03-10); e a máquina de situação com o desligamento (BL-M03-07, BL-M03-08, CA-2, CA-3).
/// </summary>
public class ServicoAlunos(
    IAlunosRepositorio repositorio,
    ITurmasRepositorio turmas,
    IConfiguracaoRepositorio configuracao,
    IAutorizacao autorizacao,
    IEscopoDeLeitura escopo,
    PoliticaSaude politicaSaude,
    IUsuarioAtual usuarioAtual,
    RegistroDeAlteracoes registro,
    IRelogioBrasil relogio,
    IValidator<NovoAlunoRequisicao> validadorNovo,
    IValidator<DadosAlunoRequisicao> validadorDados,
    IValidator<SaudeAlunoRequisicao> validadorSaude,
    IValidator<MudarSituacaoRequisicao> validadorSituacao)
{
    public const string Entidade = nameof(Aluno);
    private static readonly CultureInfo PtBr = new("pt-BR");

    public async Task<Pagina<AlunoNaLista>> ListarAsync(FiltroAlunos filtro, int pagina, CancellationToken ct = default)
    {
        var alcance = await escopo.TurmasAsync(Recurso.CadastroAlunosResponsaveis, ct);
        return await repositorio.ListarAsync(filtro, alcance, Pagina<AlunoNaLista>.Normalizar(pagina), Pagina<AlunoNaLista>.TamanhoPadrao, ct);
    }

    /// <summary>Os alunos em trial com prazo vencendo (até o aviso do parâmetro) ou vencido — sinal na tela, sem mensagem (P12).</summary>
    public async Task<(IReadOnlyList<(Guid Id, string Nome, DateOnly TrialAte)> Vencendo, IReadOnlyList<(Guid Id, string Nome, DateOnly TrialAte)> Vencidos)>
        TrialsAsync(int diasAviso, CancellationToken ct = default)
    {
        var alcance = await escopo.TurmasAsync(Recurso.CadastroAlunosResponsaveis, ct);
        var hoje = DateOnly.FromDateTime(relogio.Hoje());
        var trials = (await repositorio.TrialsAsync(alcance, ct)).Where(t => t.TrialAte is not null)
            .Select(t => (t.Id, t.Nome, TrialAte: t.TrialAte!.Value)).OrderBy(t => t.TrialAte).ToList();
        return (trials.Where(t => t.TrialAte >= hoje && t.TrialAte.DayNumber - hoje.DayNumber <= diasAviso).ToList(),
            trials.Where(t => t.TrialAte < hoje).ToList());
    }

    public async Task<FichaAluno?> ObterFichaAsync(Guid id, CancellationToken ct = default)
    {
        var alvo = new AlvoEscopo(TipoAlvoEscopo.Aluno, id);
        if (!await autorizacao.PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Ler, alvo, ct)) return null;
        if (await repositorio.ObterAlunoAsync(id, ct) is not { } aluno) return null;

        var hoje = DateOnly.FromDateTime(relogio.Hoje());
        var gestao = autorizacao.Nivel(Recurso.CadastroAlunosResponsaveis) == NivelAcesso.Total;
        var verValores = autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Ler);
        var financeiro = autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Alterar);
        var editar = await autorizacao.PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar, alvo, ct);
        var acessoSaude = await politicaSaude.AcessoAsync(id, ct);
        var editarSaude = acessoSaude == AcessoSaude.Completo
                          && await autorizacao.PodeAsync(Recurso.DocumentosObservacoesMedicas, Operacao.Alterar, alvo, ct);

        var responsaveis = new List<ResponsavelDaFicha>();
        foreach (var v in (await repositorio.VinculosDoAlunoAsync(id, ct)).OrderByDescending(v => v.Pagador).ThenBy(v => v.Responsavel!.Nome))
        {
            // Os irmãos só aparecem para quem vê cada um: o professor não fica sabendo de aluno de outra turma.
            var outros = new List<string>();
            foreach (var o in (await repositorio.VinculosDoResponsavelAsync(v.Responsavel_ID, ct)).Where(o => o.Aluno_ID != id))
                if (await autorizacao.PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Ler, new AlvoEscopo(TipoAlvoEscopo.Aluno, o.Aluno_ID), ct))
                    outros.Add(o.Aluno!.Nome);
            outros.Sort(StringComparer.CurrentCulture);
            var r = v.Responsavel!;
            responsaveis.Add(new ResponsavelDaFicha(v.VinculoAlunoResponsavel_ID, r.Responsavel_ID, r.Nome, gestao ? r.Cpf : null,
                r.Telefone, r.Email, v.Parentesco, v.Pagador, v.RecebeCobranca, v.RecebeNotificacao, outros));
        }

        var matriculas = (await repositorio.MatriculasDoAlunoAsync(id, ct))
            .OrderBy(m => m.Situacao).ThenByDescending(m => m.DataInicio)
            .Select(m => new MatriculaDaFicha(m.Matricula_ID, m.Turma_ID, m.Turma?.Nome ?? "—", m.DataInicio, m.DataFim,
                verValores ? m.Mensalidade : null, m.Situacao, m.MotivoEncerramento))
            .ToList();

        var consentimentos = await repositorio.ConsentimentosDoAlunoAsync(id, ct);
        var estados = Enum.GetValues<FinalidadeConsentimento>()
            .ToDictionary(f => f, f => ConsentimentoAluno.EstadoAtual(consentimentos, f));
        var historico = gestao ? await configuracao.ListarAlteracoesDoRegistroAsync(Entidade, id, 60, ct) : [];

        var pode = new PermissoesFicha(
            EditarDados: editar,
            EditarSaude: editarSaude,
            MudarSituacao: financeiro,
            Matricular: financeiro,
            EditarVinculos: financeiro,
            Consentimentos: editar,
            VerValores: verValores,
            VerHistorico: gestao,
            VerDocumentos: gestao);

        return new FichaAluno(aluno.Aluno_ID, aluno.Nome, aluno.DataNascimento, aluno.Idade(hoje), gestao ? aluno.Cpf : null,
            aluno.Categoria, aluno.Endereco, aluno.Situacao, aluno.SituacaoDesde, aluno.TrialAte, aluno.DiasParaFimDoTrial(hoje),
            aluno.TrialVencido(hoje), aluno.DataMatricula, aluno.DataDesligamento, aluno.MotivoDesligamento, Cadastros.Versao(aluno.Versao),
            responsaveis, matriculas, acessoSaude, PoliticaSaude.Filtrar(aluno, acessoSaude), estados,
            consentimentos.OrderByDescending(c => c.RegistradoEm).Select(c => new ConsentimentoDaFicha(c.ConsentimentoAluno_ID,
                c.Finalidade, c.Estado, c.VersaoTermo, c.Responsavel?.Nome ?? "—", c.Origem, c.RegistradoEm, c.RegistradoPorNome,
                c.RevogadoEm, c.RevogadoPorNome, c.MotivoRevogacao)).ToList(),
            historico, pode);
    }

    public async Task<ResultadoOperacao> CriarAsync(NovoAlunoRequisicao r, CancellationToken ct = default)
    {
        var validacao = await validadorNovo.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);

        if (await turmas.ObterTurmaAsync(r.TurmaId!.Value, ct) is not { Ativa: true } turma)
            return ResultadoOperacao.Erro("Turma não encontrada ou encerrada.", nameof(NovoAlunoRequisicao.TurmaId));

        // Aluno: o CPF não repete; nome + nascimento iguais é só sugestão (pode ser outra criança).
        var cpfAluno = Cpf.Normalizar(r.Cpf);
        if (cpfAluno is not null && await repositorio.AlunoPorCpfAsync(cpfAluno, ct) is { } mesmoCpf)
            return ResultadoOperacao.Erro($"Já existe o aluno {mesmoCpf.Nome} com este CPF.", nameof(NovoAlunoRequisicao.Cpf));
        if (!r.ConfirmarAlunoNovo)
        {
            var parecidos = await repositorio.AlunosComMesmoNomeENascimentoAsync(TextoHelper.NormalizarParaBusca(r.Nome), r.DataNascimento!.Value, ct);
            if (parecidos.Count > 0)
                return new ResultadoOperacao
                {
                    Sucesso = false,
                    Campo = nameof(NovoAlunoRequisicao.ConfirmarAlunoNovo),
                    Mensagem = "Já existe um aluno com este nome e esta data de nascimento. Se for outra criança, confirme para continuar.",
                    Dados = new DuplicidadeAluno(parecidos.Select(a => (a.Aluno_ID, a.Nome, a.DataNascimento)).ToList())
                };
        }

        var resolucao = await ResolverResponsaveisAsync(r.Responsaveis, ct);
        if (resolucao.Falha is { } falha) return falha;

        var agora = relogio.Agora();
        var aluno = Aluno.Novo(r.Nome, r.DataNascimento!.Value, r.DataInicio!.Value, r.EmTrial ? r.TrialAte : null);
        aluno.Aluno_ID = Guid.NewGuid();
        aluno.Cpf = cpfAluno;
        aluno.Categoria = Cadastros.Vazio(r.Categoria);
        aluno.Endereco = Cadastros.Endereco(r);
        AplicarSaude(aluno, r.Alergias, r.Restricoes, r.ObservacoesMedicas, r.ContatoEmergenciaNome, r.ContatoEmergenciaTelefone,
            r.ContatoEmergenciaParentesco);
        repositorio.AdicionarAluno(aluno);

        foreach (var (responsavel, pedido, novo) in resolucao.Resolvidos)
        {
            if (novo) repositorio.AdicionarResponsavel(responsavel);
            var vinculo = new VinculoAlunoResponsavel
            {
                VinculoAlunoResponsavel_ID = Guid.NewGuid(), Aluno_ID = aluno.Aluno_ID, Responsavel_ID = responsavel.Responsavel_ID,
                Parentesco = pedido.Parentesco, RecebeNotificacao = pedido.RecebeNotificacao
            };
            vinculo.DefinirFinanceiro(pedido.Pagador, pedido.RecebeCobranca);
            repositorio.AdicionarVinculo(vinculo);
        }

        var mensalidade = r.Mensalidade ?? turma.Mensalidade;
        repositorio.AdicionarMatricula(new Matricula
        {
            Matricula_ID = Guid.NewGuid(), Aluno_ID = aluno.Aluno_ID, Turma_ID = turma.Turma_ID, DataInicio = r.DataInicio.Value, Mensalidade = mensalidade
        });

        foreach (var c in r.Consentimentos.Where(c => c.Resposta != RespostaConsentimento.NaoColetado))
            repositorio.AdicionarConsentimento(NovoConsentimento(aluno.Aluno_ID, c.Finalidade, c.Resposta == RespostaConsentimento.Concedido,
                resolucao.PorIndice[c.ResponsavelIndice].Responsavel_ID, c.Origem, agora));

        var descricaoResponsaveis = string.Join("; ", resolucao.Resolvidos.Select(x =>
            $"{x.Responsavel.Nome} ({VinculoAlunoResponsavel.Rotulo(x.Pedido.Parentesco).ToLowerInvariant()}" +
            (x.Pedido.Pagador ? ", paga a mensalidade" : string.Empty) + (x.Novo ? string.Empty : ", cadastro reaproveitado") + ")"));
        registro.Registrar(Entidade, aluno.Aluno_ID, "Matrícula", null,
            $"{(aluno.Situacao == SituacaoAluno.Trial ? $"Trial até {aluno.TrialAte:dd/MM/yyyy}" : "Ativo")} em {turma.Nome} desde " +
            $"{r.DataInicio:dd/MM/yyyy}, mensalidade {Moeda(mensalidade)}" +
            (mensalidade != turma.Mensalidade ? $" (a da turma é {Moeda(turma.Mensalidade)})" : string.Empty) +
            $". Responsáveis: {descricaoResponsaveis}.");

        var avisos = new List<string>(resolucao.Avisos);
        if (await AvisoCapacidadeAsync(turma, 1, ct) is { } capacidade) avisos.Add(capacidade);

        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok(string.Join(" ", new[] { $"Matrícula de {aluno.Nome} em {turma.Nome} registrada." }.Concat(avisos)), aluno.Aluno_ID);
    }

    public async Task<DadosAlunoRequisicao?> DadosParaEdicaoAsync(Guid id, CancellationToken ct = default)
    {
        if (!await autorizacao.PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar, new AlvoEscopo(TipoAlvoEscopo.Aluno, id), ct))
            return null;
        if (await repositorio.ObterAlunoAsync(id, ct) is not { } a) return null;
        var r = new DadosAlunoRequisicao
        {
            Id = a.Aluno_ID, Nome = a.Nome, DataNascimento = a.DataNascimento, Cpf = Cpf.Formatar(a.Cpf), Categoria = a.Categoria,
            Versao = Cadastros.Versao(a.Versao)
        };
        Cadastros.PreencherEndereco(r, a.Endereco);
        return r;
    }

    public async Task<ResultadoOperacao> EditarDadosAsync(DadosAlunoRequisicao r, CancellationToken ct = default)
    {
        var validacao = await validadorDados.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (!await autorizacao.PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar, new AlvoEscopo(TipoAlvoEscopo.Aluno, r.Id), ct)
            || await repositorio.ObterAlunoAsync(r.Id, ct) is not { } aluno)
            return ResultadoOperacao.Erro("Aluno não encontrado.");
        if (Cadastros.LerVersao(r.Versao) is { } versao) repositorio.DefinirVersaoOriginal(aluno, versao);

        var cpf = Cpf.Normalizar(r.Cpf);
        if (cpf is not null && await repositorio.AlunoPorCpfAsync(cpf, ct) is { } outro && outro.Aluno_ID != aluno.Aluno_ID)
            return ResultadoOperacao.Erro($"Este CPF já é do aluno {outro.Nome}.", nameof(DadosAlunoRequisicao.Cpf));

        var antes = Descrever(aluno);
        aluno.DefinirNome(r.Nome);
        aluno.DataNascimento = r.DataNascimento!.Value;
        aluno.Cpf = cpf;
        aluno.Categoria = Cadastros.Vazio(r.Categoria);
        aluno.Endereco = Cadastros.Endereco(r);
        if (!registro.SeMudou(Entidade, aluno.Aluno_ID, "Dados", antes, Descrever(aluno))) return ResultadoOperacao.Ok("Nada mudou.");
        return await SalvarComConflitoAsync($"Dados de {aluno.Nome} atualizados.", ct);
    }

    public async Task<SaudeAlunoRequisicao?> SaudeParaEdicaoAsync(Guid id, CancellationToken ct = default)
    {
        if (await politicaSaude.AcessoAsync(id, ct) != AcessoSaude.Completo
            || !await autorizacao.PodeAsync(Recurso.DocumentosObservacoesMedicas, Operacao.Alterar, new AlvoEscopo(TipoAlvoEscopo.Aluno, id), ct)
            || await repositorio.ObterAlunoAsync(id, ct) is not { } a)
            return null;
        return new SaudeAlunoRequisicao
        {
            Id = a.Aluno_ID, Alergias = a.Alergias, Restricoes = a.Restricoes, ObservacoesMedicas = a.ObservacoesMedicas,
            ContatoEmergenciaNome = a.ContatoEmergenciaNome, ContatoEmergenciaTelefone = Cadastros.TelefoneParaCampo(a.ContatoEmergenciaTelefone),
            ContatoEmergenciaParentesco = a.ContatoEmergenciaParentesco, Versao = Cadastros.Versao(a.Versao)
        };
    }

    /// <summary>Saúde e contato de emergência (BL-M03-11). O registro de alterações anota que mudou, nunca o conteúdo (R5).</summary>
    public async Task<ResultadoOperacao> EditarSaudeAsync(SaudeAlunoRequisicao r, CancellationToken ct = default)
    {
        var validacao = await validadorSaude.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await politicaSaude.AcessoAsync(r.Id, ct) != AcessoSaude.Completo
            || !await autorizacao.PodeAsync(Recurso.DocumentosObservacoesMedicas, Operacao.Alterar, new AlvoEscopo(TipoAlvoEscopo.Aluno, r.Id), ct)
            || await repositorio.ObterAlunoAsync(r.Id, ct) is not { } aluno)
            return ResultadoOperacao.Erro("Aluno não encontrado.");
        if (Cadastros.LerVersao(r.Versao) is { } versao) repositorio.DefinirVersaoOriginal(aluno, versao);

        var antes = FotografiaSaude(aluno);
        AplicarSaude(aluno, r.Alergias, r.Restricoes, r.ObservacoesMedicas, r.ContatoEmergenciaNome, r.ContatoEmergenciaTelefone,
            r.ContatoEmergenciaParentesco);
        if (antes == FotografiaSaude(aluno)) return ResultadoOperacao.Ok("Nada mudou.");
        registro.Registrar(Entidade, aluno.Aluno_ID, "Saúde", null, "Saúde e contato de emergência atualizados (conteúdo restrito).");
        return await SalvarComConflitoAsync($"Saúde de {aluno.Nome} atualizada.", ct);
    }

    /// <summary>
    /// Uma transição da máquina do aluno (BL-M03-07), sempre por ação de alguém — nenhuma é automática.
    /// Desligar exige data e motivo, encerra as matrículas (a base da recorrência de cobrança, S4) e libera as vagas,
    /// preservando o histórico (BL-M03-08, CA-2, CA-3). Rematricular cria a matrícula nova com a nova data.
    /// </summary>
    public async Task<ResultadoOperacao> MudarSituacaoAsync(MudarSituacaoRequisicao r, CancellationToken ct = default)
    {
        if (!autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Alterar))
            return ResultadoOperacao.Erro("Só o gestor muda a situação do aluno (ela liga e desliga a cobrança).");
        var validacao = await validadorSituacao.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await repositorio.ObterAlunoAsync(r.AlunoId, ct) is not { } aluno) return ResultadoOperacao.Erro("Aluno não encontrado.");

        var hoje = DateOnly.FromDateTime(relogio.Hoje());
        var data = r.Data!.Value;
        var de = aluno.Situacao;

        // Só o prazo do trial (sem mudar de situação).
        if (r.Para == de && de == SituacaoAluno.Trial && r.TrialAte is { } novoPrazo)
        {
            var prazoAntes = aluno.TrialAte;
            aluno.AlterarPrazoTrial(novoPrazo);
            if (!registro.SeMudou(Entidade, aluno.Aluno_ID, "Prazo do trial", prazoAntes?.ToString("dd/MM/yyyy", PtBr),
                    novoPrazo.ToString("dd/MM/yyyy", PtBr), r.Motivo))
                return ResultadoOperacao.Ok("Nada mudou.");
            await repositorio.SalvarAsync(ct);
            return ResultadoOperacao.Ok($"Trial de {aluno.Nome} vai até {novoPrazo:dd/MM/yyyy}.");
        }

        if (!MaquinaSituacaoAluno.Permite(de, r.Para))
            return ResultadoOperacao.Erro($"Não dá para ir de {MaquinaSituacaoAluno.Rotulo(de)} para {MaquinaSituacaoAluno.Rotulo(r.Para)}.");

        var ativas = (await repositorio.MatriculasDoAlunoAsync(aluno.Aluno_ID, ct)).Where(m => m.Ativa).ToList();
        Turma? turmaRematricula = null;
        if (r.Para == SituacaoAluno.Desistente)
        {
            if (data > hoje) return ResultadoOperacao.Erro("O desligamento não pode ter data futura.", nameof(r.Data));
            if (data < aluno.DataMatricula)
                return ResultadoOperacao.Erro($"O desligamento não pode ser antes da matrícula ({aluno.DataMatricula:dd/MM/yyyy}).", nameof(r.Data));
            if (data < aluno.SituacaoDesde)
                return ResultadoOperacao.Erro(
                    $"O desligamento não pode ser antes de {aluno.SituacaoDesde:dd/MM/yyyy} (início da situação atual: {MaquinaSituacaoAluno.Rotulo(de)}).",
                    nameof(r.Data));
            if (ativas.FirstOrDefault(m => m.DataInicio > data) is { } posterior)
                return ResultadoOperacao.Erro(
                    $"A matrícula em {posterior.Turma?.Nome} começa em {posterior.DataInicio:dd/MM/yyyy}, depois da data do desligamento.", nameof(r.Data));
        }
        else if (de == SituacaoAluno.Desistente)
        {
            if (r.TurmaId is not { } turmaId || await turmas.ObterTurmaAsync(turmaId, ct) is not { Ativa: true } turma)
                return ResultadoOperacao.Erro("Escolha a turma da rematrícula.", nameof(r.TurmaId));
            if (aluno.DataDesligamento is { } desligado && data < desligado)
                return ResultadoOperacao.Erro($"A rematrícula não pode ser antes do desligamento ({desligado:dd/MM/yyyy}).", nameof(r.Data));
            turmaRematricula = turma;
        }
        else
        {
            if (data > hoje) return ResultadoOperacao.Erro("A data não pode ser futura.", nameof(r.Data));
            if (data < aluno.SituacaoDesde)
                return ResultadoOperacao.Erro($"A data não pode ser antes de {aluno.SituacaoDesde:dd/MM/yyyy} (início da situação atual).", nameof(r.Data));
        }

        aluno.MudarSituacao(r.Para, data, r.Motivo);
        var detalhe = string.Empty;
        if (r.Para == SituacaoAluno.Desistente)
        {
            foreach (var matricula in ativas) matricula.Encerrar(data, $"Desligamento: {r.Motivo!.Trim()}");
            detalhe = ativas.Count == 0 ? " Não havia matrícula ativa." : $" {ativas.Count} matrícula(s) encerrada(s) e vaga(s) liberada(s).";
        }
        else if (turmaRematricula is not null)
        {
            var mensalidade = r.Mensalidade ?? turmaRematricula.Mensalidade;
            repositorio.AdicionarMatricula(new Matricula
            {
                Matricula_ID = Guid.NewGuid(), Aluno_ID = aluno.Aluno_ID, Turma_ID = turmaRematricula.Turma_ID, DataInicio = data, Mensalidade = mensalidade
            });
            detalhe = $" Nova matrícula em {turmaRematricula.Nome}, mensalidade {Moeda(mensalidade)}.";
            if (await AvisoCapacidadeAsync(turmaRematricula, 1, ct) is { } aviso) detalhe += " " + aviso;
        }

        registro.Registrar(Entidade, aluno.Aluno_ID, "Situação", MaquinaSituacaoAluno.Rotulo(de),
            $"{MaquinaSituacaoAluno.Rotulo(r.Para)} em {data:dd/MM/yyyy}.{detalhe}", r.Motivo);
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Situação de {aluno.Nome}: {MaquinaSituacaoAluno.Rotulo(r.Para)} desde {data:dd/MM/yyyy}.{detalhe}");
    }

    internal record ResolucaoResponsaveis(
        IReadOnlyList<(Responsavel Responsavel, ResponsavelDoAlunoRequisicao Pedido, bool Novo)> Resolvidos,
        IReadOnlyDictionary<int, Responsavel> PorIndice,
        IReadOnlyList<string> Avisos,
        ResultadoOperacao? Falha);

    /// <summary>
    /// Deduplicação no primeiro cadastro (BL-M03-01, CA-1): escolhido na busca ou com o mesmo CPF, o cadastro é reaproveitado;
    /// mesmo telefone ou mesmo nome só sugerem (celular compartilhado) e param a gravação até quem cadastra decidir.
    /// </summary>
    internal async Task<ResolucaoResponsaveis> ResolverResponsaveisAsync(IReadOnlyList<ResponsavelDoAlunoRequisicao> pedidos, CancellationToken ct)
    {
        var resolvidos = new List<(Responsavel, ResponsavelDoAlunoRequisicao, bool)>();
        var porIndice = new Dictionary<int, Responsavel>();
        var avisos = new List<string>();

        for (var i = 0; i < pedidos.Count; i++)
        {
            var pedido = pedidos[i];
            if (!pedido.Preenchido) continue;
            var campo = $"Responsaveis[{i}].Nome";
            Responsavel? responsavel;
            var novo = false;

            if (pedido.ResponsavelId is { } escolhido)
            {
                responsavel = await repositorio.ObterResponsavelAsync(escolhido, ct);
                if (responsavel is null) return Falhou(ResultadoOperacao.Erro("Responsável não encontrado.", campo));
            }
            else
            {
                var cpf = Cpf.Normalizar(pedido.Cpf);
                responsavel = cpf is null ? null : await repositorio.ResponsavelPorCpfAsync(cpf, ct);
                if (responsavel is not null)
                {
                    avisos.Add($"O cadastro de {responsavel.Nome} já existia (mesmo CPF) e foi reaproveitado, sem duplicidade.");
                }
                else
                {
                    if (!pedido.ConfirmarNovo)
                    {
                        var parecidos = await repositorio.ResponsaveisParecidosAsync(Telefone.ParaE164(pedido.Telefone),
                            TextoHelper.NormalizarParaBusca(pedido.Nome), ct);
                        if (parecidos.Count > 0)
                            return Falhou(new ResultadoOperacao
                            {
                                Sucesso = false,
                                Campo = $"Responsaveis[{i}].ConfirmarNovo",
                                Mensagem = $"Encontramos cadastros parecidos com {pedido.Nome?.Trim()} (mesmo celular ou mesmo nome). " +
                                           "Use o cadastro existente ou confirme que é outra pessoa.",
                                Dados = new DuplicidadeResponsavel(i, parecidos.Select(p => p with { Cpf = CpfMascarado.De(p.Cpf) }).ToList())
                            });
                    }

                    responsavel = new Responsavel
                    {
                        Responsavel_ID = Guid.NewGuid(),
                        Cpf = cpf,
                        Telefone = Telefone.ParaE164(pedido.Telefone),
                        Email = Cadastros.Vazio(pedido.Email)?.ToLowerInvariant()
                    };
                    responsavel.DefinirNome(pedido.Nome!);
                    novo = true;
                }
            }

            if (resolvidos.Any(x => x.Item1.Responsavel_ID == responsavel.Responsavel_ID))
                return Falhou(ResultadoOperacao.Erro($"{responsavel.Nome} foi informado duas vezes.", campo));
            resolvidos.Add((responsavel, pedido, novo));
            porIndice[i] = responsavel;
        }

        return new ResolucaoResponsaveis(resolvidos, porIndice, avisos, null);

        ResolucaoResponsaveis Falhou(ResultadoOperacao falha) => new([], new Dictionary<int, Responsavel>(), [], falha);
    }

    internal ConsentimentoAluno NovoConsentimento(Guid alunoId, FinalidadeConsentimento finalidade, bool concedido, Guid responsavelId,
        OrigemConsentimento origem, DateTime agora) => new()
    {
        ConsentimentoAluno_ID = Guid.NewGuid(),
        Aluno_ID = alunoId,
        Finalidade = finalidade,
        Concedido = concedido,
        VersaoTermo = TermosConsentimento.Obter(finalidade).Versao,
        Responsavel_ID = responsavelId,
        Origem = origem,
        RegistradoEm = agora,
        RegistradoPor_ID = usuarioAtual.Id,
        RegistradoPorNome = TextoHelper.Truncar(usuarioAtual.Nome ?? "Sistema", ConsentimentoAluno.TamanhoAutor)!
    };

    /// <summary>Sinalizar, não bloquear: a matrícula entra mesmo com a turma cheia, com o aviso.</summary>
    internal async Task<string?> AvisoCapacidadeAsync(Turma turma, int entrando, CancellationToken ct)
    {
        var ocupadas = (await turmas.OcupacaoAsync(ct)).GetValueOrDefault(turma.Turma_ID) + entrando;
        return ocupadas > turma.Capacidade
            ? $"Atenção: {turma.Nome} passou da capacidade ({ocupadas} de {turma.Capacidade} vagas)."
            : null;
    }

    private async Task<ResultadoOperacao> SalvarComConflitoAsync(string mensagem, CancellationToken ct)
    {
        try
        {
            await repositorio.SalvarAsync(ct);
            return ResultadoOperacao.Ok(mensagem);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            return ResultadoOperacao.Erro(Cadastros.MensagemConflito);
        }
    }

    private static void AplicarSaude(Aluno aluno, string? alergias, string? restricoes, string? observacoes,
        string? contatoNome, string? contatoTelefone, string? contatoParentesco)
    {
        aluno.Alergias = Cadastros.Vazio(alergias);
        aluno.Restricoes = Cadastros.Vazio(restricoes);
        aluno.ObservacoesMedicas = Cadastros.Vazio(observacoes);
        aluno.ContatoEmergenciaNome = Cadastros.Vazio(contatoNome);
        aluno.ContatoEmergenciaTelefone = Telefone.ParaE164(contatoTelefone);
        aluno.ContatoEmergenciaParentesco = Cadastros.Vazio(contatoParentesco);
    }

    private static string FotografiaSaude(Aluno a) =>
        string.Join("|", a.Alergias, a.Restricoes, a.ObservacoesMedicas, a.ContatoEmergenciaNome, a.ContatoEmergenciaTelefone,
            a.ContatoEmergenciaParentesco);

    /// <summary>Os dados cadastrais (sem saúde: o registro de alterações não guarda dado sensível).</summary>
    private static string Descrever(Aluno a) =>
        $"{a.Nome} · nascimento {a.DataNascimento.ToString("dd/MM/yyyy", PtBr)} · CPF {(a.Cpf is null ? "não informado" : Cpf.Formatar(a.Cpf))}" +
        $" · categoria {a.Categoria ?? "—"} · {(string.IsNullOrEmpty(a.Endereco.Resumo()) ? "sem endereço" : a.Endereco.Resumo())}";

    internal static string Moeda(decimal valor) => valor.ToString("C", PtBr);
}
