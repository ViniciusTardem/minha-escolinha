namespace MinhaEscolinha.Application.Comum;

/// <summary>
/// O e-mail do sistema, no padrão do AlternativaDepartamentoPessoal: o link de nova senha (fora do motor: é da própria
/// pessoa, não é aviso) e os termos de consentimento (D111), que saem pelo motor único como um gatilho do catálogo (R3).
/// </summary>
public interface IEnvioEmail
{
    string Descricao { get; }
    Task EnviarAsync(string para, string assunto, string corpoHtml, CancellationToken ct = default);
}

/// <summary>Opções do e-mail (<c>Email:*</c>). A senha do SMTP só em user-secrets ou variável de ambiente.</summary>
public class OpcoesEmail
{
    public const string Secao = "Email";
    public const string ModoCaixaLocal = "CaixaLocal";
    public const string ModoSmtp = "Smtp";

    /// <summary>"CaixaLocal" (padrão: grava o e-mail em disco, nada sai da máquina) ou "Smtp".</summary>
    public string Modo { get; set; } = ModoCaixaLocal;

    /// <summary>Onde a caixa local grava os e-mails (padrão: %LOCALAPPDATA%\MinhaEscolinha\emails).</summary>
    public string? Pasta { get; set; }

    public string? SmtpHost { get; set; }
    public int SmtpPorta { get; set; } = 587;
    public string? SmtpUsuario { get; set; }
    public string? SmtpSenha { get; set; }
    public bool SmtpSsl { get; set; } = true;
    public string? Remetente { get; set; }

    /// <summary>Fora de Production, o SMTP manda tudo para este endereço (obrigatório): nada chega a quem usa o sistema.</summary>
    public string? RedirecionarPara { get; set; }

    /// <summary>O endereço https do sistema: vai no link de nova senha.</summary>
    public string UrlPublica { get; set; } = "https://localhost:7186";

    /// <summary>Desligado (caixa local em produção): a recuperação fica só com o link gerado pela gestão.</summary>
    public bool Habilitado { get; set; } = true;

    public bool Smtp => string.Equals(Modo, ModoSmtp, StringComparison.OrdinalIgnoreCase);

    public string LinkNovaSenha(string token) => $"{UrlPublica.TrimEnd('/')}/Conta/NovaSenha/{token}";

    /// <summary>A página pública de aceite dos termos (D111).</summary>
    public string LinkTermos(string token) => $"{UrlPublica.TrimEnd('/')}/Termos/{token}";
}

/// <summary>O e-mail de nova senha sai por job (regra de ouro 2: nada externo dentro do request).</summary>
public interface IAgendadorEmail
{
    void EnviarRecuperacaoSenha(Guid recuperacaoId);
}
