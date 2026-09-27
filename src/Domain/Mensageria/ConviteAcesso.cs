using System.Security.Cryptography;
using System.Text;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Mensageria;

public enum TipoConvite
{
    Responsavel = 10,
    Professor = 20
}

public enum SituacaoConvite
{
    /// <summary>Emitido e ainda válido (até <see cref="ConviteAcesso.ExpiraEm"/>).</summary>
    Pendente = 10,

    /// <summary>A pessoa se identificou e ativou o acesso.</summary>
    Aceito = 20,

    Cancelado = 30,

    /// <summary>Reenviado: vale o convite novo, com outro link.</summary>
    Substituido = 40
}

/// <summary>
/// Convite de acesso sobre um cadastro que já existe na escolinha (BL-M01-04): o responsável ou o professor recebe um link
/// de uso único pelo WhatsApp e ativa o acesso se identificando pelo CPF (BL-M01-05). O link carrega um segredo aleatório;
/// o banco guarda só o hash dele. Reenviar cria outro convite (outro link) e deixa o anterior substituído.
/// </summary>
public class ConviteAcesso : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int ValidadeDias = 7;
    public const int MaxTentativasIdentificacao = 5;
    public const int TamanhoHash = 64;
    public const int TamanhoMotivo = 300;

    public Guid ConviteAcesso_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public TipoConvite Tipo { get; set; }

    /// <summary>A ficha convidada (uma das duas, conforme o tipo).</summary>
    public Guid? Responsavel_ID { get; set; }
    public Guid? Professor_ID { get; set; }

    /// <summary>Nome e telefone da ficha no momento do convite.</summary>
    public string Nome { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;

    /// <summary>SHA-256 do segredo do link, em hexadecimal. Único no sistema todo.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiraEm { get; set; }
    public SituacaoConvite Situacao { get; set; } = SituacaoConvite.Pendente;

    public DateTime EmitidoEm { get; set; }
    public Guid? EmitidoPor_ID { get; set; }
    public string EmitidoPorNome { get; set; } = string.Empty;

    public int TentativasIdentificacao { get; set; }
    public DateTime? AceitoEm { get; set; }

    /// <summary>A pessoa que ativou (identidade global).</summary>
    public Guid? Pessoa_ID { get; set; }

    public DateTime? EncerradoEm { get; set; }
    public string? MotivoEncerramento { get; set; }

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    public Guid FichaId => Tipo == TipoConvite.Responsavel ? Responsavel_ID!.Value : Professor_ID!.Value;

    public bool Expirado(DateTime agora) => Situacao == SituacaoConvite.Pendente && agora >= ExpiraEm;

    public bool Valido(DateTime agora) => Situacao == SituacaoConvite.Pendente && agora < ExpiraEm;

    /// <summary>Convite novo com um segredo aleatório. Devolve o segredo, que só existe no link (nunca no banco).</summary>
    public static (ConviteAcesso Convite, string Token) Emitir(TipoConvite tipo, Guid fichaId, string nome, string telefone, DateTime agora,
        Guid? autorId, string autorNome)
    {
        var token = GerarToken();
        var convite = new ConviteAcesso
        {
            ConviteAcesso_ID = Guid.NewGuid(),
            Tipo = tipo,
            Responsavel_ID = tipo == TipoConvite.Responsavel ? fichaId : null,
            Professor_ID = tipo == TipoConvite.Professor ? fichaId : null,
            Nome = nome,
            Telefone = telefone,
            TokenHash = Hash(token),
            ExpiraEm = agora.AddDays(ValidadeDias),
            EmitidoEm = agora,
            EmitidoPor_ID = autorId,
            EmitidoPorNome = autorNome
        };
        return (convite, token);
    }

    /// <summary>CPF errado na ativação: conta a tentativa; na 5ª o convite é cancelado (o gestor reenvia).</summary>
    public void RegistrarTentativaErrada(DateTime agora)
    {
        TentativasIdentificacao++;
        if (TentativasIdentificacao >= MaxTentativasIdentificacao)
            Encerrar(SituacaoConvite.Cancelado, agora, "Tentativas de identificação esgotadas: peça um novo convite à escolinha.");
    }

    public void Aceitar(Guid pessoaId, DateTime agora)
    {
        if (!Valido(agora)) throw new InvalidOperationException("Convite fora da validade.");
        Situacao = SituacaoConvite.Aceito;
        AceitoEm = agora;
        Pessoa_ID = pessoaId;
    }

    public void Encerrar(SituacaoConvite situacao, DateTime agora, string motivo)
    {
        if (Situacao != SituacaoConvite.Pendente) return;
        Situacao = situacao;
        EncerradoEm = agora;
        MotivoEncerramento = TextoHelper.Truncar(motivo, TamanhoMotivo);
    }

    public static string GerarToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public static string Rotulo(SituacaoConvite situacao) => situacao switch
    {
        SituacaoConvite.Pendente => "Enviado",
        SituacaoConvite.Aceito => "Aceito",
        SituacaoConvite.Cancelado => "Cancelado",
        SituacaoConvite.Substituido => "Substituído",
        _ => situacao.ToString()
    };
}

/// <summary>
/// Índice global do que saiu pelo WhatsApp: o id da mensagem na Meta (wamid) e o telefone levam à escolinha. É o que o
/// webhook usa para achar a escolinha antes de tudo (plano §3.2), sem atravessar o filtro de escolinha.
/// </summary>
public class RastreioWhatsApp
{
    public const int TamanhoIdExterno = 150;
    public const int TamanhoChaveTelefone = 20;

    public Guid RastreioWhatsApp_ID { get; set; }
    public string IdExterno { get; set; } = string.Empty;

    /// <summary><see cref="Telefone.ChaveWhatsApp"/> do destinatário: casa a resposta de texto livre com o 9º dígito ou sem ele.</summary>
    public string ChaveTelefone { get; set; } = string.Empty;

    public Guid Escolinha_ID { get; set; }
    public Guid Mensagem_ID { get; set; }
    public DateTime EnviadoEm { get; set; }
}
