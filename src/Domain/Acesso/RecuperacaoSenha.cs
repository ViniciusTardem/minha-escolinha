using System.Security.Cryptography;
using System.Text;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Acesso;

/// <summary>Como o link de nova senha chegou à pessoa (BL-M01-09).</summary>
public enum CanalRecuperacao
{
    /// <summary>A pessoa pediu na tela de login, e o link foi para o e-mail do login.</summary>
    Email = 10,

    /// <summary>A gestão gerou na ficha e repassou pelo canal que quis (quem entra só pelo CPF, sem e-mail).</summary>
    LinkDaGestao = 20
}

/// <summary>
/// Um pedido de nova senha (BL-M01-09): um link de uso único, com validade, guardado só pelo hash (o segredo não fica no
/// banco). Global, como a credencial: a senha é da pessoa, não da escolinha. Enquanto o e-mail não sai, o segredo fica
/// cifrado (Data Protection) só para o job montar o link; depois do envio, sai do banco.
/// </summary>
public class RecuperacaoSenha
{
    public const int TamanhoHash = 64;
    public const int TamanhoAutor = 150;
    public static readonly TimeSpan ValidadeEmail = TimeSpan.FromHours(2);
    public static readonly TimeSpan ValidadeLinkDaGestao = TimeSpan.FromHours(24);

    public Guid RecuperacaoSenha_ID { get; set; }
    public Guid Pessoa_ID { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public CanalRecuperacao Canal { get; set; }
    public DateTime CriadaEm { get; set; }
    public DateTime ExpiraEm { get; set; }
    public DateTime? UsadaEm { get; set; }

    /// <summary>Quem gerou o link (a gestão) e em qual escolinha; nulo quando a própria pessoa pediu.</summary>
    public string? CriadaPorNome { get; set; }

    public Guid? Escolinha_ID { get; set; }

    /// <summary>O segredo cifrado, só até o e-mail sair (o job precisa dele para montar o link).</summary>
    public string? TokenProtegido { get; set; }

    public DateTime? EmailEnviadoEm { get; set; }

    public static (RecuperacaoSenha Recuperacao, string Token) Nova(Guid pessoaId, CanalRecuperacao canal, DateTime agora,
        string? autor = null, Guid? escolinhaId = null)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var recuperacao = new RecuperacaoSenha
        {
            RecuperacaoSenha_ID = Guid.NewGuid(),
            Pessoa_ID = pessoaId,
            TokenHash = Hash(token),
            Canal = canal,
            CriadaEm = agora,
            ExpiraEm = agora.Add(canal == CanalRecuperacao.Email ? ValidadeEmail : ValidadeLinkDaGestao),
            CriadaPorNome = TextoHelper.Truncar(autor, TamanhoAutor),
            Escolinha_ID = escolinhaId
        };
        return (recuperacao, token);
    }

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public bool Valida(DateTime agora) => UsadaEm is null && agora < ExpiraEm;

    public void Usar(DateTime agora)
    {
        if (!Valida(agora)) throw new InvalidOperationException("O link de nova senha já foi usado ou venceu.");
        UsadaEm = agora;
        TokenProtegido = null;
    }

    public void MarcarEmailEnviado(DateTime agora)
    {
        EmailEnviadoEm = agora;
        TokenProtegido = null;
    }
}
