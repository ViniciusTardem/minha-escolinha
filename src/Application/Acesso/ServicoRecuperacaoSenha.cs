using FluentValidation;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Application.Acesso;

public class EsqueciSenhaRequisicao
{
    /// <summary>O e-mail ou o CPF com que a pessoa entra.</summary>
    public string Login { get; set; } = string.Empty;
}

public class NovaSenhaRequisicao
{
    public string NovaSenha { get; set; } = string.Empty;
    public string ConfirmacaoSenha { get; set; } = string.Empty;
}

public class NovaSenhaRequisicaoValidador : AbstractValidator<NovaSenhaRequisicao>
{
    public NovaSenhaRequisicaoValidador()
    {
        RuleFor(x => x.NovaSenha).SenhaForte();
        RuleFor(x => x.ConfirmacaoSenha).Equal(x => x.NovaSenha).WithMessage("A confirmação não confere com a nova senha.");
    }
}

/// <summary>O link de nova senha que a gestão gerou, para mostrar uma vez e repassar.</summary>
public sealed record LinkNovaSenha(string Nome, string Link, DateTime ExpiraEm);

/// <summary>
/// A recuperação de senha (BL-M01-09):
/// <list type="bullet">
/// <item>pelo e-mail: a pessoa pede na tela de login com o e-mail ou o CPF; se o login dela é um e-mail, um link de uso
/// único (2 horas) vai por e-mail, por job. A resposta é a mesma exista a conta ou não (não revela quem tem acesso);</item>
/// <item>pela gestão: quem entra só pelo CPF, sem e-mail, recebe da gestão um link de uso único (24 horas), gerado na
/// ficha e repassado pelo canal que a gestão quiser. Registrado com o autor. A gestão só gera o link de quem atua apenas
/// nesta escolinha: a pessoa que também atua em outra (ou é da plataforma) recupera pelo e-mail ou pela equipe LDNA.</item>
/// </list>
/// Nova senha definida: as sessões abertas caem (carimbo de segurança) e o bloqueio por tentativas acaba.
/// </summary>
public class ServicoRecuperacaoSenha(
    IAcessoRepositorio repositorio,
    IConvitesRepositorio convites,
    IHashSenha hashSenha,
    IProtetorSegredos protetor,
    IEnvioEmail email,
    IAgendadorEmail agendador,
    OpcoesEmail opcoes,
    IAutorizacao autorizacao,
    IContextoEscolinha contexto,
    IUsuarioAtual usuarioAtual,
    RegistroDeAlteracoes registro,
    IRelogioBrasil relogio,
    IValidator<NovaSenhaRequisicao> validador,
    ILogger<ServicoRecuperacaoSenha> logger)
{
    public const string RespostaPedido =
        "Se houver uma conta com esse login e um e-mail cadastrado, o link de nova senha chega em alguns minutos. Quem entra só pelo CPF pede o link à escolinha.";

    public async Task<ResultadoOperacao> PedirPorEmailAsync(EsqueciSenhaRequisicao r, CancellationToken ct = default)
    {
        var login = Credencial.NormalizarLogin(r.Login);
        if (login.Length == 0) return ResultadoOperacao.Erro("Informe o e-mail ou o CPF com que você entra.", nameof(EsqueciSenhaRequisicao.Login));

        var cpf = login.Contains('@') ? null : Cpf.Normalizar(login);
        var credencial = cpf is not null
            ? await repositorio.ObterCredencialPorCpfAsync(cpf, ct)
            : await repositorio.ObterCredencialPorLoginAsync(login, ct);
        if (credencial is not { Ativa: true } || !credencial.Login.Contains('@') || !opcoes.Habilitado)
        {
            logger.LogInformation("Pedido de nova senha sem e-mail a enviar ({Login}).", ParaLog(login));
            return ResultadoOperacao.Ok(RespostaPedido);
        }

        var (recuperacao, token) = RecuperacaoSenha.Nova(credencial.Pessoa_ID, CanalRecuperacao.Email, relogio.Agora());
        recuperacao.TokenProtegido = protetor.Proteger(token);
        repositorio.AdicionarRecuperacao(recuperacao);
        await repositorio.SalvarAsync(ct);
        agendador.EnviarRecuperacaoSenha(recuperacao.RecuperacaoSenha_ID);
        logger.LogInformation("Pedido de nova senha de {Login}: e-mail na fila.", ParaLog(login));
        return ResultadoOperacao.Ok(RespostaPedido);
    }

    /// <summary>O job: monta o link com o segredo cifrado e manda o e-mail. Relê tudo antes (regra de ouro 4).</summary>
    /// <exception cref="Exception">Falha do SMTP: o job tenta de novo.</exception>
    public async Task<string> EnviarEmailAsync(Guid recuperacaoId, CancellationToken ct = default)
    {
        if (await repositorio.ObterRecuperacaoAsync(recuperacaoId, ct) is not { } recuperacao) return "Pedido não encontrado.";
        var agora = relogio.Agora();
        if (recuperacao.EmailEnviadoEm is not null || recuperacao.TokenProtegido is null) return "O e-mail já saiu.";
        if (!recuperacao.Valida(agora)) return "O link já venceu ou foi usado: nada a enviar.";
        if (await repositorio.ObterCredencialAsync(recuperacao.Pessoa_ID, ct) is not { Ativa: true } credencial || !credencial.Login.Contains('@'))
            return "Sem e-mail para enviar.";

        if (protetor.Desproteger(recuperacao.TokenProtegido) is not { } segredo) return "O segredo do link não pôde ser lido: a pessoa pede de novo.";
        var link = opcoes.LinkNovaSenha(segredo);
        var nome = credencial.Pessoa?.Nome ?? "Olá";
        await email.EnviarAsync(credencial.Login, "Minha Escolinha: link para criar uma nova senha",
            $"""
             <p>Olá, {System.Net.WebUtility.HtmlEncode(nome)}!</p>
             <p>Recebemos um pedido para criar uma nova senha no Minha Escolinha. Use o link abaixo até {recuperacao.ExpiraEm:dd/MM/yyyy} às {recuperacao.ExpiraEm:HH:mm}:</p>
             <p><a href="{link}">{link}</a></p>
             <p>O link vale uma vez só. Se você não pediu, ignore este e-mail: a sua senha continua a mesma.</p>
             """, ct);
        recuperacao.MarcarEmailEnviado(agora);
        await repositorio.SalvarAsync(ct);
        logger.LogInformation("E-mail de nova senha enviado para {Login} ({Canal}).", ParaLog(credencial.Login), email.Descricao);
        return "E-mail enviado.";
    }

    /// <summary>A gestão gera, na ficha de quem já tem acesso, um link de nova senha de uso único (24 horas).</summary>
    public async Task<ResultadoOperacao> GerarLinkPelaGestaoAsync(TipoConvite tipo, Guid fichaId, CancellationToken ct = default)
    {
        if (!autorizacao.Pode(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)) return ResultadoOperacao.Erro("Só a gestão gera o link de nova senha.");
        Guid? pessoaId;
        string nome;
        if (tipo == TipoConvite.Responsavel)
        {
            if (await convites.ObterResponsavelAsync(fichaId, ct) is not { } responsavel) return ResultadoOperacao.Erro("Responsável não encontrado.");
            (pessoaId, nome) = (responsavel.Pessoa_ID, responsavel.Nome);
        }
        else
        {
            if (await convites.ObterProfessorAsync(fichaId, ct) is not { } professor) return ResultadoOperacao.Erro("Professor não encontrado.");
            (pessoaId, nome) = (professor.Pessoa_ID, professor.Nome);
        }
        if (pessoaId is not { } pessoa) return ResultadoOperacao.Erro("Esta pessoa ainda não ativou o acesso: mande o convite.");
        if (await repositorio.ObterCredencialAsync(pessoa, ct) is not { Ativa: true } credencial)
            return ResultadoOperacao.Erro("O acesso desta pessoa não está ativo.");

        // A senha vale em todas as escolinhas da pessoa: a gestão de uma só gera o link de quem atua só nela.
        var escolinhas = await repositorio.EscolinhasDaPessoaAsync(pessoa, ct);
        if (credencial.AdministradorPlataforma || escolinhas.Any(e => e.EscolinhaId != contexto.EscolinhaId))
            return ResultadoOperacao.Erro("Esta pessoa também atua em outra escolinha: ela recupera a senha pelo e-mail, ou com a equipe da plataforma.");

        var agora = relogio.Agora();
        var (recuperacao, token) = RecuperacaoSenha.Nova(pessoa, CanalRecuperacao.LinkDaGestao, agora, usuarioAtual.Nome ?? "Gestão",
            contexto.EscolinhaId);
        repositorio.AdicionarRecuperacao(recuperacao);
        registro.Registrar(tipo == TipoConvite.Responsavel ? ServicoResponsaveis.Entidade : ServicoProfessores.Entidade, fichaId, "Link de nova senha", null,
            $"Gerado, vale até {recuperacao.ExpiraEm:dd/MM/yyyy HH:mm}");
        await repositorio.SalvarAsync(ct);
        logger.LogInformation("{Autor} gerou um link de nova senha para {Login}.", usuarioAtual.Nome, ParaLog(credencial.Login));
        return ResultadoOperacao.Ok("Link gerado.", new LinkNovaSenha(nome, opcoes.LinkNovaSenha(token), recuperacao.ExpiraEm));
    }

    /// <summary>O nome de quem abriu o link, se ele ainda vale (a tela de nova senha).</summary>
    public async Task<string?> ValidarAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || await repositorio.RecuperacaoPorHashAsync(RecuperacaoSenha.Hash(token), ct) is not { } recuperacao
            || !recuperacao.Valida(relogio.Agora()))
            return null;
        return (await repositorio.ObterCredencialAsync(recuperacao.Pessoa_ID, ct))?.Pessoa?.Nome ?? "Olá";
    }

    public async Task<ResultadoOperacao> DefinirNovaSenhaAsync(string token, NovaSenhaRequisicao r, CancellationToken ct = default)
    {
        var validacao = await validador.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        var agora = relogio.Agora();
        if (string.IsNullOrWhiteSpace(token) || await repositorio.RecuperacaoPorHashAsync(RecuperacaoSenha.Hash(token), ct) is not { } recuperacao
            || !recuperacao.Valida(agora))
            return ResultadoOperacao.Erro("Este link já foi usado ou venceu. Peça outro.");
        if (await repositorio.ObterCredencialAsync(recuperacao.Pessoa_ID, ct) is not { Ativa: true } credencial)
            return ResultadoOperacao.Erro("Este acesso não está ativo. Fale com a escolinha.");

        credencial.DefinirSenha(hashSenha.Gerar(r.NovaSenha), trocarNoProximoAcesso: false);
        recuperacao.Usar(agora);
        await repositorio.SalvarAsync(ct);
        logger.LogInformation("{Login} criou uma nova senha pelo link ({Canal}).", ParaLog(credencial.Login), recuperacao.Canal);
        return ResultadoOperacao.Ok("Nova senha criada. Entre com ela.");
    }

    /// <summary>O CPF não vai inteiro para o log (D52).</summary>
    private static string ParaLog(string login) =>
        Cpf.Normalizar(login) is { } cpf ? $"***.{cpf[3..6]}.{cpf[6..9]}-**" : login;
}
