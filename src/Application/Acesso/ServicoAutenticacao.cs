using FluentValidation;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Acesso;

namespace MinhaEscolinha.Application.Acesso;

public interface IServicoAutenticacao
{
    /// <summary>Dados = <see cref="CredencialAutenticada"/> em caso de sucesso.</summary>
    Task<ResultadoOperacao> AutenticarAsync(LoginRequisicao requisicao, CancellationToken ct = default);

    /// <summary>
    /// Revalida a sessão do cookie: devolve a credencial atualizada ou <c>null</c> se a sessão deve cair
    /// (credencial inativa, excluída ou carimbo de segurança diferente).
    /// </summary>
    Task<CredencialAutenticada?> ValidarSessaoAsync(Guid pessoaId, Guid carimboSeguranca, CancellationToken ct = default);

    /// <summary>Troca a senha de quem está logado. Dados = <see cref="CredencialAutenticada"/> (novo carimbo, para renovar o cookie).</summary>
    Task<ResultadoOperacao> TrocarPropriaSenhaAsync(TrocarSenhaRequisicao requisicao, CancellationToken ct = default);

    /// <summary>Confere a senha de quem está logado antes de uma ação sensível (ex.: trocar a conta de recebimento).</summary>
    Task<bool> ConferirSenhaAsync(string senha, CancellationToken ct = default);

    /// <summary>As escolinhas em que quem está logado tem vínculo ativo (para escolher ou trocar a escolinha da sessão).</summary>
    Task<IReadOnlyList<EscolinhaDaPessoa>> EscolinhasDisponiveisAsync(CancellationToken ct = default);
}

/// <summary>
/// Login, sessão e troca de senha. Regras portadas do <c>UsuarioServico</c> do AlternativaDepartamentoPessoal
/// (bloqueio após 5 falhas, mesma mensagem para login inexistente e senha errada, verificação fictícia para não revelar
/// quais logins existem), aplicadas à credencial única da pessoa.
/// </summary>
public class ServicoAutenticacao : IServicoAutenticacao
{
    private const string MensagemCredenciaisInvalidas = "Login ou senha inválidos.";

    private readonly IAcessoRepositorio _repositorio;
    private readonly IHashSenha _hashSenha;
    private readonly IRelogioBrasil _relogio;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IValidator<LoginRequisicao> _validadorLogin;
    private readonly IValidator<TrocarSenhaRequisicao> _validadorTrocarSenha;
    private readonly ILogger<ServicoAutenticacao> _logger;

    // Hash de uma senha qualquer: quando o login não existe, a verificação roda assim mesmo,
    // para o tempo de resposta não revelar quais logins existem.
    private readonly Lazy<string> _hashFicticio;

    public ServicoAutenticacao(
        IAcessoRepositorio repositorio,
        IHashSenha hashSenha,
        IRelogioBrasil relogio,
        IUsuarioAtual usuarioAtual,
        IValidator<LoginRequisicao> validadorLogin,
        IValidator<TrocarSenhaRequisicao> validadorTrocarSenha,
        ILogger<ServicoAutenticacao> logger)
    {
        _repositorio = repositorio;
        _hashSenha = hashSenha;
        _relogio = relogio;
        _usuarioAtual = usuarioAtual;
        _validadorLogin = validadorLogin;
        _validadorTrocarSenha = validadorTrocarSenha;
        _logger = logger;
        _hashFicticio = new Lazy<string>(() => _hashSenha.Gerar(Guid.NewGuid().ToString()));
    }

    public async Task<ResultadoOperacao> AutenticarAsync(LoginRequisicao requisicao, CancellationToken ct = default)
    {
        var validacao = await _validadorLogin.ValidateAsync(requisicao, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);

        var login = Credencial.NormalizarLogin(requisicao.Login);
        var credencial = await _repositorio.ObterCredencialPorLoginAsync(login, ct);
        var agora = _relogio.Agora();

        if (credencial is null)
        {
            _hashSenha.Verificar(requisicao.Senha, _hashFicticio.Value);
            _logger.LogWarning("Login recusado: login {Login} não existe.", login);
            return ResultadoOperacao.Erro(MensagemCredenciaisInvalidas);
        }

        if (credencial.EstaBloqueado(agora))
        {
            _logger.LogWarning("Login recusado: {Login} bloqueado até {BloqueadoAte}.", login, credencial.BloqueadoAte);
            return ResultadoOperacao.Erro(
                $"Acesso bloqueado por excesso de tentativas. Tente de novo a partir de {credencial.BloqueadoAte:HH:mm}.");
        }

        if (!_hashSenha.Verificar(requisicao.Senha, credencial.SenhaHash))
        {
            credencial.RegistrarFalhaLogin(agora);
            await _repositorio.SalvarAsync(ct);

            if (credencial.EstaBloqueado(agora))
            {
                _logger.LogWarning("Login {Login} bloqueado até {BloqueadoAte} após {Max} falhas.",
                    login, credencial.BloqueadoAte, Credencial.MaxTentativasFalhas);
                return ResultadoOperacao.Erro(
                    $"Acesso bloqueado por excesso de tentativas. Tente de novo a partir de {credencial.BloqueadoAte:HH:mm}.");
            }

            _logger.LogWarning("Login recusado: senha incorreta para {Login} ({Tentativas}ª falha).", login, credencial.TentativasFalhas);
            return ResultadoOperacao.Erro(MensagemCredenciaisInvalidas);
        }

        // Senha certa de credencial inativa: mesma mensagem genérica (não revela que o login existe).
        if (!credencial.Ativa)
        {
            _logger.LogWarning("Login recusado: {Login} está inativo.", login);
            return ResultadoOperacao.Erro(MensagemCredenciaisInvalidas);
        }

        var escolinhas = await _repositorio.EscolinhasDaPessoaAsync(credencial.Pessoa_ID, ct);
        if (escolinhas.Count == 0 && !credencial.AdministradorPlataforma)
        {
            _logger.LogWarning("Login recusado: {Login} não tem vínculo ativo em nenhuma escolinha.", login);
            return ResultadoOperacao.Erro("O seu acesso não está ativo em nenhuma escolinha. Fale com a escolinha.");
        }

        credencial.RegistrarLoginComSucesso(agora);
        await _repositorio.SalvarAsync(ct);

        _logger.LogInformation("Login de {Login} ({Escolinhas} escolinha(s), plataforma: {Plataforma}).",
            login, escolinhas.Count, credencial.AdministradorPlataforma);
        return ResultadoOperacao.Ok("Login realizado.", ParaAutenticada(credencial, escolinhas));
    }

    public async Task<CredencialAutenticada?> ValidarSessaoAsync(Guid pessoaId, Guid carimboSeguranca, CancellationToken ct = default)
    {
        var credencial = await _repositorio.ObterCredencialAsync(pessoaId, ct);
        if (credencial is null || !credencial.Ativa || credencial.CarimboSeguranca != carimboSeguranca) return null;
        var escolinhas = await _repositorio.EscolinhasDaPessoaAsync(pessoaId, ct);
        return ParaAutenticada(credencial, escolinhas);
    }

    public async Task<ResultadoOperacao> TrocarPropriaSenhaAsync(TrocarSenhaRequisicao requisicao, CancellationToken ct = default)
    {
        if (_usuarioAtual.Id is not { } pessoaId) return ResultadoOperacao.Erro("Sessão expirada. Entre novamente.");

        var validacao = await _validadorTrocarSenha.ValidateAsync(requisicao, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);

        var credencial = await _repositorio.ObterCredencialAsync(pessoaId, ct);
        if (credencial is null || !credencial.Ativa) return ResultadoOperacao.Erro("Sessão expirada. Entre novamente.");

        if (!_hashSenha.Verificar(requisicao.SenhaAtual, credencial.SenhaHash))
            return ResultadoOperacao.Erro("A senha atual não confere.", nameof(TrocarSenhaRequisicao.SenhaAtual));

        credencial.DefinirSenha(_hashSenha.Gerar(requisicao.NovaSenha), trocarNoProximoAcesso: false);
        await _repositorio.SalvarAsync(ct);

        _logger.LogInformation("{Login} trocou a própria senha.", credencial.Login);
        var escolinhas = await _repositorio.EscolinhasDaPessoaAsync(pessoaId, ct);
        return ResultadoOperacao.Ok("Senha alterada.", ParaAutenticada(credencial, escolinhas));
    }

    public async Task<bool> ConferirSenhaAsync(string senha, CancellationToken ct = default)
    {
        if (_usuarioAtual.Id is not { } pessoaId || string.IsNullOrEmpty(senha)) return false;
        var credencial = await _repositorio.ObterCredencialAsync(pessoaId, ct);
        return credencial is { Ativa: true } && _hashSenha.Verificar(senha, credencial.SenhaHash);
    }

    public async Task<IReadOnlyList<EscolinhaDaPessoa>> EscolinhasDisponiveisAsync(CancellationToken ct = default) =>
        _usuarioAtual.Id is { } pessoaId ? await _repositorio.EscolinhasDaPessoaAsync(pessoaId, ct) : [];

    private static CredencialAutenticada ParaAutenticada(Credencial c, IReadOnlyList<EscolinhaDaPessoa> escolinhas) =>
        new(c.Pessoa_ID, c.Pessoa?.Nome ?? c.Login, c.Login, c.AdministradorPlataforma, c.TrocarSenha, c.CarimboSeguranca, escolinhas);
}
