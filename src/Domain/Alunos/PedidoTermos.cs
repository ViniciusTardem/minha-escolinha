using System.Security.Cryptography;
using System.Text;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Alunos;

public enum SituacaoPedidoTermos
{
    /// <summary>O link está valendo (até a validade).</summary>
    Aguardando = 10,

    /// <summary>O responsável respondeu pelo link: o link deixou de existir.</summary>
    Respondido = 20,

    /// <summary>A gestão enviou outro link, ou os termos foram registrados por outro caminho.</summary>
    Cancelado = 30
}

/// <summary>
/// O pedido de aceite dos termos de consentimento (D111): um link de uso único para o 1º responsável do aluno responder,
/// em separado, o tratamento de dados e o uso de imagem (R6). O banco guarda só o hash do segredo, para achar o pedido;
/// o segredo cifrado (Data Protection) fica enquanto o link vale, para a gestão copiar o link na ficha de quem não tem
/// e-mail. Respondido, cancelado ou vencido, o link deixa de existir; respondido ou cancelado, o segredo sai do banco.
/// </summary>
public class PedidoTermos : IEntidadeDaEscolinha
{
    public const int TamanhoHash = 64;
    public const int TamanhoAutor = 150;
    public const int TamanhoMotivo = 300;
    public const int TamanhoIp = 45;

    public Guid PedidoTermos_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Aluno_ID { get; set; }
    public Guid Responsavel_ID { get; set; }
    public Responsavel? Responsavel { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    /// <summary>O segredo cifrado, só enquanto o link vale.</summary>
    public string? TokenProtegido { get; set; }

    /// <summary>O que o link pede: só as finalidades que ainda não tinham registro.</summary>
    public bool PedeDados { get; set; }
    public bool PedeImagem { get; set; }

    public SituacaoPedidoTermos Situacao { get; set; } = SituacaoPedidoTermos.Aguardando;
    public DateTime CriadoEm { get; set; }
    public DateTime ExpiraEm { get; set; }
    public string CriadoPorNome { get; set; } = string.Empty;

    /// <summary>O e-mail para onde o link foi (nulo: a gestão repassa o link).</summary>
    public string? EnviadoPara { get; set; }

    public DateTime? RespondidoEm { get; set; }
    public bool? AceitouDados { get; set; }
    public bool? AceitouImagem { get; set; }

    /// <summary>De onde veio a resposta (evidência do aceite).</summary>
    public string? RespondidoDoIp { get; set; }

    public DateTime? CanceladoEm { get; set; }
    public string? MotivoCancelamento { get; set; }

    /// <summary>Concorrência otimista (rowversion): duas respostas ao mesmo link não passam juntas.</summary>
    public byte[] Versao { get; set; } = [];

    public static (PedidoTermos Pedido, string Token) Novo(Guid alunoId, Guid responsavelId, bool pedeDados, bool pedeImagem,
        DateTime agora, int validadeDias, string autor, string? email)
    {
        if (!pedeDados && !pedeImagem) throw new InvalidOperationException("O pedido precisa de ao menos um termo.");
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var pedido = new PedidoTermos
        {
            PedidoTermos_ID = Guid.NewGuid(),
            Aluno_ID = alunoId,
            Responsavel_ID = responsavelId,
            TokenHash = Hash(token),
            PedeDados = pedeDados,
            PedeImagem = pedeImagem,
            CriadoEm = agora,
            ExpiraEm = agora.AddDays(Math.Max(1, validadeDias)),
            CriadoPorNome = TextoHelper.Truncar(autor, TamanhoAutor) ?? string.Empty,
            EnviadoPara = string.IsNullOrWhiteSpace(email) ? null : email
        };
        return (pedido, token);
    }

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public bool Vencido(DateTime agora) => Situacao == SituacaoPedidoTermos.Aguardando && agora >= ExpiraEm;

    public bool Valido(DateTime agora) => Situacao == SituacaoPedidoTermos.Aguardando && agora < ExpiraEm;

    /// <summary>A resposta do responsável. Cada termo pedido precisa de sim ou não (R6: um não depende do outro).</summary>
    public void Responder(bool? dados, bool? imagem, DateTime agora, string? ip)
    {
        if (!Valido(agora)) throw new InvalidOperationException("O link dos termos não vale mais.");
        if ((PedeDados && dados is null) || (PedeImagem && imagem is null))
            throw new InvalidOperationException("Responda cada termo pedido.");
        Situacao = SituacaoPedidoTermos.Respondido;
        RespondidoEm = agora;
        AceitouDados = PedeDados ? dados : null;
        AceitouImagem = PedeImagem ? imagem : null;
        RespondidoDoIp = TextoHelper.Truncar(ip, TamanhoIp);
        TokenProtegido = null;
    }

    public void Cancelar(string motivo, DateTime agora)
    {
        if (Situacao != SituacaoPedidoTermos.Aguardando) return;
        Situacao = SituacaoPedidoTermos.Cancelado;
        CanceladoEm = agora;
        MotivoCancelamento = TextoHelper.Truncar(motivo, TamanhoMotivo);
        TokenProtegido = null;
    }
}
