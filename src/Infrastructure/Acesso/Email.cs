using System.Net;
using System.Net.Mail;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Infrastructure.Hangfire;

namespace MinhaEscolinha.Infrastructure.Acesso;

/// <summary>Grava cada e-mail como .html numa pasta local (padrão do DP): desenvolvimento e homologação sem SMTP.</summary>
public class EnvioEmailCaixaLocal(OpcoesEmail opcoes, IRelogioBrasil relogio, ILogger<EnvioEmailCaixaLocal> logger) : IEnvioEmail
{
    private string Pasta => opcoes.Pasta
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MinhaEscolinha", "emails");

    public string Descricao => $"caixa local ({Pasta})";

    public async Task EnviarAsync(string para, string assunto, string corpoHtml, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Pasta);
        var nome = $"{relogio.Agora():yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.html";
        await File.WriteAllTextAsync(Path.Combine(Pasta, nome),
            $"<!-- Para: {WebUtility.HtmlEncode(para)} -->\n<h2>{WebUtility.HtmlEncode(assunto)}</h2>\n{corpoHtml}", ct);
        logger.LogInformation("E-mail gravado na caixa local: {Arquivo}.", nome);
    }
}

/// <summary>SMTP (padrão do DP). Fora de Production, tudo vai para <see cref="OpcoesEmail.RedirecionarPara"/>.</summary>
public class EnvioEmailSmtp(OpcoesEmail opcoes) : IEnvioEmail
{
    public string Descricao => $"SMTP {opcoes.SmtpHost}";

    public async Task EnviarAsync(string para, string assunto, string corpoHtml, CancellationToken ct = default)
    {
        var destino = string.IsNullOrWhiteSpace(opcoes.RedirecionarPara) ? para : opcoes.RedirecionarPara;
        if (destino != para) assunto = $"[teste: era para {para}] {assunto}";

        using var mensagem = new MailMessage(opcoes.Remetente ?? opcoes.SmtpUsuario ?? "nao-responda@localhost", destino, assunto, corpoHtml)
            { IsBodyHtml = true };
        using var cliente = new SmtpClient(opcoes.SmtpHost, opcoes.SmtpPorta) { EnableSsl = opcoes.SmtpSsl };
        if (!string.IsNullOrWhiteSpace(opcoes.SmtpUsuario)) cliente.Credentials = new NetworkCredential(opcoes.SmtpUsuario, opcoes.SmtpSenha);
        await cliente.SendMailAsync(mensagem, ct);
    }
}

/// <summary>
/// A trava do e-mail, no padrão das outras integrações:
/// <list type="bullet">
/// <item>caixa local em Production: o e-mail fica desligado, com aviso (a recuperação fica só com o link da gestão);</item>
/// <item>SMTP exige <c>SmtpHost</c> e <c>Remetente</c>; fora de Production, exige também <c>RedirecionarPara</c>;</item>
/// <item>em Production, <c>UrlPublica</c> precisa ser https e não pode ser localhost (vai no link).</item>
/// </list>
/// </summary>
public static class GuardaEmail
{
    public static void Validar(string ambiente, OpcoesEmail opcoes, Action<string>? aviso = null)
    {
        var producao = string.Equals(ambiente, "Production", StringComparison.OrdinalIgnoreCase);
        if (!opcoes.Smtp)
        {
            if (producao)
            {
                opcoes.Habilitado = false;
                aviso?.Invoke("Email:Modo = CaixaLocal em Production: o e-mail de nova senha fica desligado; a recuperação fica com o link gerado pela gestão.");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(opcoes.SmtpHost) || string.IsNullOrWhiteSpace(opcoes.Remetente))
                throw new InvalidOperationException("Email:Modo = Smtp exige Email:SmtpHost e Email:Remetente.");
            if (!producao && string.IsNullOrWhiteSpace(opcoes.RedirecionarPara))
                throw new InvalidOperationException(
                    "Fora de Production, Email:Modo = Smtp exige Email:RedirecionarPara: nenhum e-mail de teste chega a quem usa o sistema.");
            if (producao) opcoes.RedirecionarPara = null;
        }

        if (producao && (!Uri.TryCreate(opcoes.UrlPublica, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps || url.IsLoopback))
            aviso?.Invoke("Email:UrlPublica precisa ser o endereço https do site: o link de nova senha usa este endereço.");
    }

    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuracao, string ambiente,
        Action<string>? aviso = null)
    {
        var opcoes = configuracao.GetSection(OpcoesEmail.Secao).Get<OpcoesEmail>() ?? new OpcoesEmail();
        Validar(ambiente, opcoes, aviso);
        services.AddSingleton(opcoes);
        if (opcoes.Smtp) services.AddScoped<IEnvioEmail, EnvioEmailSmtp>();
        else services.AddScoped<IEnvioEmail, EnvioEmailCaixaLocal>();
        services.AddScoped<IAgendadorEmail, AgendadorEmailHangfire>();
        services.AddScoped<JobsAcesso>();
        return services;
    }
}

/// <summary>Os jobs do acesso: o e-mail de nova senha (global, fora de escolinha).</summary>
public class JobsAcesso(ServicoRecuperacaoSenha recuperacao, ILogger<JobsAcesso> logger)
{
    [Queue(HangfireEscolinhaExtensions.FilaIntegracoes)]
    [AutomaticRetry(Attempts = 5)]
    public async Task EnviarRecuperacaoSenhaAsync(Guid recuperacaoId, CancellationToken ct)
    {
        var resultado = await recuperacao.EnviarEmailAsync(recuperacaoId, ct);
        logger.LogInformation("Nova senha {Recuperacao}: {Resultado}", recuperacaoId, resultado);
    }
}

public class AgendadorEmailHangfire(IBackgroundJobClient jobs) : IAgendadorEmail
{
    public void EnviarRecuperacaoSenha(Guid recuperacaoId) =>
        jobs.Enqueue<JobsAcesso>(j => j.EnviarRecuperacaoSenhaAsync(recuperacaoId, CancellationToken.None));
}
