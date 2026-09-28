using FluentValidation;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Application.Alunos;

/// <summary>
/// Responsáveis (BL-M03-01) e vínculos com os alunos (BL-M03-03). A busca e a deduplicação reaproveitam o cadastro entre
/// irmãos (CA-1). As marcações de cobrança e de pagador são financeiras: só o gestor muda.
/// </summary>
public class ServicoResponsaveis(
    IAlunosRepositorio repositorio,
    IAutorizacao autorizacao,
    IEscopoDeLeitura escopo,
    ServicoAlunos alunos,
    RegistroDeAlteracoes registro,
    IRelogioBrasil relogio,
    IUsuarioAtual usuarioAtual,
    IValidator<ResponsavelRequisicao> validador,
    IValidator<ResponsavelDoAlunoRequisicao> validadorDoAluno,
    IValidator<VinculoRequisicao> validadorVinculo,
    IContextoEscolinha contexto,
    Financeiro.IAgendadorFinanceiro agendadorFinanceiro)
{
    public const string Entidade = nameof(Responsavel);

    /// <summary>Busca para reaproveitar no cadastro (só quem cadastra aluno: o gestor). CPF sai mascarado.</summary>
    public async Task<IReadOnlyList<ResponsavelEncontrado>> BuscarAsync(string? termo, CancellationToken ct = default)
    {
        if (autorizacao.Nivel(Recurso.CadastroAlunosResponsaveis) != NivelAcesso.Total || string.IsNullOrWhiteSpace(termo) || termo.Trim().Length < 3)
            return [];
        return (await repositorio.BuscarResponsaveisAsync(termo.Trim(), 8, ct))
            .Select(r => r with { Cpf = CpfMascarado.De(r.Cpf) }).ToList();
    }

    /// <summary>Os responsáveis já escolhidos num formulário que voltou com erro (para a tela mostrar quem é cada um).</summary>
    public async Task<IReadOnlyDictionary<Guid, ResponsavelEncontrado>> EscolhidosAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        if (autorizacao.Nivel(Recurso.CadastroAlunosResponsaveis) != NivelAcesso.Total) return new Dictionary<Guid, ResponsavelEncontrado>();
        return (await repositorio.ResponsaveisAsync(ids.Distinct().ToList(), ct))
            .ToDictionary(r => r.Id, r => r with { Cpf = CpfMascarado.De(r.Cpf) });
    }

    public async Task<Pagina<ResponsavelNaLista>> ListarAsync(string? busca, int pagina, CancellationToken ct = default)
    {
        var alcance = await escopo.TurmasAsync(Recurso.CadastroAlunosResponsaveis, ct);
        return await repositorio.ListarResponsaveisAsync(busca, alcance, Pagina<ResponsavelNaLista>.Normalizar(pagina),
            Pagina<ResponsavelNaLista>.TamanhoPadrao, ct);
    }

    public async Task<FichaResponsavel?> ObterFichaAsync(Guid id, CancellationToken ct = default)
    {
        if (await repositorio.ObterResponsavelAsync(id, ct) is not { } r) return null;
        var vinculos = await repositorio.VinculosDoResponsavelAsync(id, ct);
        if (!await NoEscopoAsync(vinculos, Operacao.Ler, ct)) return null;

        var gestao = autorizacao.Nivel(Recurso.CadastroAlunosResponsaveis) == NivelAcesso.Total;
        // Só os filhos que quem pergunta enxerga (o professor, os das turmas dele).
        var visiveis = new List<VinculoAlunoResponsavel>();
        foreach (var v in vinculos)
            if (gestao || await autorizacao.PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Ler, new AlvoEscopo(TipoAlvoEscopo.Aluno, v.Aluno_ID), ct))
                visiveis.Add(v);
        return new FichaResponsavel(r.Responsavel_ID, r.Nome, gestao ? r.Cpf : null, r.Telefone, r.Email, r.Endereco, r.Pessoa_ID is not null,
            visiveis.OrderBy(v => v.Aluno!.Nome).Select(v => (v.Aluno_ID, v.Aluno!.Nome, v.Parentesco, v.Pagador, v.RecebeCobranca,
                v.RecebeNotificacao, v.Aluno.Situacao, v.Verificacao)).ToList(),
            await NoEscopoAsync(vinculos, Operacao.Alterar, ct),
            autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Ler));
    }

    public async Task<ResponsavelRequisicao?> ParaEdicaoAsync(Guid id, CancellationToken ct = default)
    {
        if (await repositorio.ObterResponsavelAsync(id, ct) is not { } r) return null;
        if (!await NoEscopoAsync(await repositorio.VinculosDoResponsavelAsync(id, ct), Operacao.Alterar, ct)) return null;
        var requisicao = new ResponsavelRequisicao
        {
            Id = r.Responsavel_ID, Nome = r.Nome, Cpf = Cpf.Formatar(r.Cpf), Telefone = Cadastros.TelefoneParaCampo(r.Telefone), Email = r.Email
        };
        Cadastros.PreencherEndereco(requisicao, r.Endereco);
        return requisicao;
    }

    public async Task<ResultadoOperacao> EditarAsync(ResponsavelRequisicao r, CancellationToken ct = default)
    {
        var validacao = await validador.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await repositorio.ObterResponsavelAsync(r.Id, ct) is not { } responsavel
            || !await NoEscopoAsync(await repositorio.VinculosDoResponsavelAsync(r.Id, ct), Operacao.Alterar, ct))
            return ResultadoOperacao.Erro("Responsável não encontrado.");

        var cpf = Cpf.Normalizar(r.Cpf);
        if (cpf is not null && await repositorio.ResponsavelPorCpfAsync(cpf, ct) is { } outro && outro.Responsavel_ID != responsavel.Responsavel_ID)
            return ResultadoOperacao.Erro($"Este CPF já é de {outro.Nome}. Se for a mesma pessoa, vincule o aluno ao cadastro dela.",
                nameof(ResponsavelRequisicao.Cpf));
        if (responsavel.Pessoa_ID is not null && responsavel.Cpf is not null && cpf != responsavel.Cpf)
            return ResultadoOperacao.Erro("O CPF de quem já ativou o acesso não muda por aqui.", nameof(ResponsavelRequisicao.Cpf));

        var antes = Descrever(responsavel);
        var paraAsaas = (responsavel.Nome, responsavel.Cpf, responsavel.Telefone, responsavel.Email);
        responsavel.DefinirNome(r.Nome);
        responsavel.Cpf = cpf;
        responsavel.Telefone = Telefone.ParaE164(r.Telefone);
        responsavel.Email = Cadastros.Vazio(r.Email)?.ToLowerInvariant();
        responsavel.Endereco = Cadastros.Endereco(r);
        if (!registro.SeMudou(Entidade, responsavel.Responsavel_ID, "Cadastro", antes, Descrever(responsavel)))
            return ResultadoOperacao.Ok("Nada mudou.");
        await repositorio.SalvarAsync(ct);
        // P42: o que a Asaas usa na fatura mudou: o cliente dela recebe os dados novos, por job (regra de ouro 2).
        if (paraAsaas != (responsavel.Nome, responsavel.Cpf, responsavel.Telefone, responsavel.Email) && contexto.EscolinhaId is { } escolinha)
            agendadorFinanceiro.AtualizarClienteAsaas(escolinha, responsavel.Responsavel_ID);
        return ResultadoOperacao.Ok($"Cadastro de {responsavel.Nome} atualizado.");
    }

    /// <summary>Mais um responsável para o aluno (já cadastrado, pelo CPF, ou novo) — a mesma deduplicação do cadastro.</summary>
    public async Task<ResultadoOperacao> VincularAsync(Guid alunoId, ResponsavelDoAlunoRequisicao pedido, CancellationToken ct = default)
    {
        if (!autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Alterar)) return ResultadoOperacao.Erro("Só o gestor mexe nos vínculos.");
        if (!pedido.Preenchido) return ResultadoOperacao.Erro("Escolha um responsável já cadastrado ou informe um novo.", "Responsaveis[0].Nome");
        var validacao = await validadorDoAluno.ValidateAsync(pedido, ct);
        if (!validacao.IsValid)
            return ResultadoOperacao.Erro(validacao.Errors[0].ErrorMessage, $"Responsaveis[0].{validacao.Errors[0].PropertyName}");
        if (await repositorio.ObterAlunoAsync(alunoId, ct) is not { } aluno) return ResultadoOperacao.Erro("Aluno não encontrado.");

        var resolucao = await alunos.ResolverResponsaveisAsync([pedido], ct);
        if (resolucao.Falha is { } falha) return falha;
        var (responsavel, _, novo) = resolucao.Resolvidos[0];

        var vinculos = await repositorio.VinculosDoAlunoAsync(alunoId, ct);
        if (vinculos.Any(v => v.Responsavel_ID == responsavel.Responsavel_ID))
            return ResultadoOperacao.Erro($"{responsavel.Nome} já é responsável por {aluno.Nome}.", "Responsaveis[0].Nome");

        if (novo) repositorio.AdicionarResponsavel(responsavel);
        if (pedido.Pagador)
            foreach (var atual in vinculos.Where(v => v.Pagador)) atual.DefinirFinanceiro(false, atual.RecebeCobranca);
        var vinculo = new VinculoAlunoResponsavel
        {
            VinculoAlunoResponsavel_ID = Guid.NewGuid(), Aluno_ID = alunoId, Responsavel_ID = responsavel.Responsavel_ID,
            Parentesco = pedido.Parentesco, RecebeNotificacao = pedido.RecebeNotificacao
        };
        vinculo.DefinirFinanceiro(pedido.Pagador, pedido.RecebeCobranca);
        if (responsavel.Pessoa_ID is not null) vinculo.ConfirmarNaCriacao(relogio.Agora(), usuarioAtual.Id, usuarioAtual.Nome ?? "Gestor");
        repositorio.AdicionarVinculo(vinculo);
        registro.Registrar(ServicoAlunos.Entidade, alunoId, "Responsável", null, DescreverVinculo(responsavel.Nome, vinculo));
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok(string.Join(" ", new[] { $"{responsavel.Nome} agora é responsável por {aluno.Nome}." }.Concat(resolucao.Avisos)));
    }

    public async Task<(VinculoAlunoResponsavel Vinculo, string Aluno)?> VinculoParaEdicaoAsync(Guid vinculoId, CancellationToken ct = default)
    {
        if (!autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Alterar)) return null;
        return await repositorio.ObterVinculoAsync(vinculoId, ct) is { } v ? (v, v.Aluno?.Nome ?? "—") : null;
    }

    /// <summary>Parentesco e permissões do vínculo. Marcar outro pagador tira a marca do anterior (um pagador por aluno).</summary>
    public async Task<ResultadoOperacao> EditarVinculoAsync(VinculoRequisicao r, CancellationToken ct = default)
    {
        if (!autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Alterar)) return ResultadoOperacao.Erro("Só o gestor mexe nos vínculos.");
        var validacao = await validadorVinculo.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await repositorio.ObterVinculoAsync(r.VinculoId, ct) is not { } vinculo) return ResultadoOperacao.Erro("Vínculo não encontrado.");

        var nome = vinculo.Responsavel?.Nome ?? "Responsável";
        var antes = DescreverVinculo(nome, vinculo);
        if (r.Pagador)
            foreach (var outro in (await repositorio.VinculosDoAlunoAsync(vinculo.Aluno_ID, ct))
                         .Where(v => v.Pagador && v.VinculoAlunoResponsavel_ID != vinculo.VinculoAlunoResponsavel_ID))
                outro.DefinirFinanceiro(false, outro.RecebeCobranca);
        vinculo.Parentesco = r.Parentesco;
        vinculo.RecebeNotificacao = r.RecebeNotificacao;
        vinculo.DefinirFinanceiro(r.Pagador, r.RecebeCobranca);
        if (!registro.SeMudou(ServicoAlunos.Entidade, vinculo.Aluno_ID, "Responsável", antes, DescreverVinculo(nome, vinculo)))
            return ResultadoOperacao.Ok("Nada mudou.");
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Vínculo de {nome} atualizado.");
    }

    public async Task<ResultadoOperacao> DesvincularAsync(Guid vinculoId, CancellationToken ct = default)
    {
        if (!autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Alterar)) return ResultadoOperacao.Erro("Só o gestor mexe nos vínculos.");
        if (await repositorio.ObterVinculoAsync(vinculoId, ct) is not { } vinculo) return ResultadoOperacao.Erro("Vínculo não encontrado.");
        var vinculos = await repositorio.VinculosDoAlunoAsync(vinculo.Aluno_ID, ct);
        if (vinculos.Count <= 1)
            return ResultadoOperacao.Erro("O aluno precisa de ao menos um responsável: toda comunicação passa por ele (R4). Vincule outro antes.");

        var nome = vinculo.Responsavel?.Nome ?? "Responsável";
        registro.Registrar(ServicoAlunos.Entidade, vinculo.Aluno_ID, "Responsável", DescreverVinculo(nome, vinculo), "Desvinculado");
        repositorio.RemoverVinculo(vinculo);
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"{nome} deixou de ser responsável por {vinculo.Aluno?.Nome}." +
                                    (vinculo.Pagador ? " Era o pagador: marque outro responsável como pagador." : string.Empty));
    }

    /// <summary>Gestor vê tudo; o professor, os responsáveis dos alunos das turmas dele.</summary>
    private async Task<bool> NoEscopoAsync(IReadOnlyList<VinculoAlunoResponsavel> vinculos, Operacao operacao, CancellationToken ct)
    {
        if (autorizacao.Nivel(Recurso.CadastroAlunosResponsaveis) == NivelAcesso.Total) return autorizacao.Pode(Recurso.CadastroAlunosResponsaveis, operacao);
        foreach (var v in vinculos)
            if (await autorizacao.PodeAsync(Recurso.CadastroAlunosResponsaveis, operacao, new AlvoEscopo(TipoAlvoEscopo.Aluno, v.Aluno_ID), ct))
                return true;
        return false;
    }

    private static string Descrever(Responsavel r) =>
        $"{r.Nome} · CPF {(r.Cpf is null ? "não informado" : Cpf.Formatar(r.Cpf))} · {r.Telefone ?? "sem celular"} · {r.Email ?? "sem e-mail"}" +
        (string.IsNullOrEmpty(r.Endereco.Resumo()) ? string.Empty : $" · {r.Endereco.Resumo()}");

    internal static string DescreverVinculo(string nome, VinculoAlunoResponsavel v)
    {
        var marcas = new List<string> { VinculoAlunoResponsavel.Rotulo(v.Parentesco).ToLowerInvariant() };
        if (v.Pagador) marcas.Add("paga a mensalidade");
        if (v.RecebeCobranca) marcas.Add("recebe cobrança");
        if (v.RecebeNotificacao) marcas.Add("recebe notificação");
        return $"{nome} ({string.Join(", ", marcas)})";
    }
}

/// <summary>
/// Consentimento na matrícula e a revogação (BL-M03-09, R6): registro separado de tratamento de dados e de uso de imagem,
/// com data, hora, finalidade, versão do termo e quem autorizou. Nada é apagado.
/// </summary>
public class ServicoConsentimentos(
    IAlunosRepositorio repositorio,
    IAutorizacao autorizacao,
    ServicoAlunos alunos,
    IRelogioBrasil relogio,
    IUsuarioAtual usuarioAtual,
    IValidator<RegistrarConsentimentoRequisicao> validador)
{
    public async Task<ResultadoOperacao> RegistrarAsync(RegistrarConsentimentoRequisicao r, CancellationToken ct = default)
    {
        var validacao = await validador.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (!await autorizacao.PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar, new AlvoEscopo(TipoAlvoEscopo.Aluno, r.AlunoId), ct))
            return ResultadoOperacao.Erro("Aluno não encontrado.");
        if (!(await repositorio.VinculosDoAlunoAsync(r.AlunoId, ct)).Any(v => v.Responsavel_ID == r.ResponsavelId))
            return ResultadoOperacao.Erro("Quem autoriza precisa ser responsável pelo aluno.", nameof(r.ResponsavelId));

        var concedido = r.Resposta == RespostaConsentimento.Concedido;
        repositorio.AdicionarConsentimento(alunos.NovoConsentimento(r.AlunoId, r.Finalidade, concedido, r.ResponsavelId!.Value, r.Origem,
            relogio.Agora()));
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"{TermosConsentimento.Rotulo(r.Finalidade)}: {(concedido ? "autorização" : "recusa")} registrada.");
    }

    /// <summary>Revoga o consentimento vigente. A revogação do uso de imagem não para a operação essencial (R6).</summary>
    public async Task<ResultadoOperacao> RevogarAsync(RevogarConsentimentoRequisicao r, CancellationToken ct = default)
    {
        if (await repositorio.ObterConsentimentoAsync(r.ConsentimentoId, ct) is not { } consentimento
            || !await autorizacao.PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar,
                new AlvoEscopo(TipoAlvoEscopo.Aluno, consentimento.Aluno_ID), ct))
            return ResultadoOperacao.Erro("Consentimento não encontrado.");

        var historico = await repositorio.ConsentimentosDoAlunoAsync(consentimento.Aluno_ID, ct);
        var atual = historico.Where(c => c.Finalidade == consentimento.Finalidade).OrderByDescending(c => c.RegistradoEm).First();
        if (atual.ConsentimentoAluno_ID != consentimento.ConsentimentoAluno_ID || consentimento.Estado != EstadoConsentimento.Concedido)
            return ResultadoOperacao.Erro("Só o consentimento vigente pode ser revogado.");

        consentimento.Revogar(relogio.Agora(), usuarioAtual.Id, usuarioAtual.Nome, Cadastros.Vazio(r.Motivo));
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"{TermosConsentimento.Rotulo(consentimento.Finalidade)}: consentimento revogado." +
                                    (consentimento.Finalidade == FinalidadeConsentimento.UsoImagem
                                        ? " Matrícula, cobrança, presença e convocação seguem normalmente."
                                        : string.Empty));
    }

    public async Task<(ConsentimentoAluno Consentimento, string Aluno)?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        if (await repositorio.ObterConsentimentoAsync(id, ct) is not { } c) return null;
        if (!await autorizacao.PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar, new AlvoEscopo(TipoAlvoEscopo.Aluno, c.Aluno_ID), ct))
            return null;
        return (c, (await repositorio.ObterAlunoAsync(c.Aluno_ID, ct))?.Nome ?? "—");
    }
}
