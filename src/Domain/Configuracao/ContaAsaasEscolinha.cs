using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Configuracao;

public enum AmbienteAsaas
{
    /// <summary>Ambiente de testes da Asaas (nenhum dinheiro de verdade).</summary>
    Sandbox = 10,

    /// <summary>Conta real: só aceita com o app rodando em Production.</summary>
    Producao = 20
}

/// <summary>
/// Conta de recebimento Asaas da escolinha (BL-M02-10, RF-FIN-002): sem ela nenhuma cobrança é emitida.
/// Na mensalidade do aluno o valor cai na conta da própria escolinha (R1).
/// A chave de API fica cifrada (Data Protection) e nunca volta para a tela — só os 4 últimos caracteres.
/// Trocar a conta pede a senha de quem troca e fica no histórico: é o ponto preferido de uma fraude.
/// </summary>
public class ContaAsaasEscolinha : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoFinal = 4;

    public Guid ContaAsaasEscolinha_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public AmbienteAsaas Ambiente { get; set; }

    /// <summary>Chave de API cifrada. Nunca em texto aberto no banco nem no log.</summary>
    public string ChaveApiProtegida { get; set; } = string.Empty;

    /// <summary>Últimos 4 caracteres da chave, para a pessoa reconhecer qual conta está ligada.</summary>
    public string FinalChave { get; set; } = string.Empty;

    public DateTime VinculadaEm { get; set; }
    public Guid? VinculadaPor_ID { get; set; }

    /// <summary>
    /// Muda a cada vínculo ou troca de chave (S4): a cobrança e o cliente guardam em qual conta foram criados, porque a
    /// chave nova não enxerga o que ficou na conta anterior.
    /// </summary>
    public Guid Identificador { get; set; }

    /// <summary>
    /// Token que a Asaas manda no cabeçalho <c>asaas-access-token</c> de cada webhook, cifrado. Nasce no primeiro vínculo e
    /// não muda na troca de conta: os webhooks da conta anterior continuam valendo para o que foi emitido nela.
    /// </summary>
    public string? TokenWebhookProtegido { get; set; }

    /// <summary>Quando o webhook foi cadastrado na conta Asaas (pela API, no modo Api), e o último erro dessa tentativa.</summary>
    public DateTime? WebhookRegistradoEm { get; set; }

    public string? WebhookErro { get; set; }

    public const int TamanhoErroWebhook = 500;

    /// <summary>Um novo identificador para a conta (vínculo ou troca de chave).</summary>
    public void NovaVersao()
    {
        Identificador = Guid.NewGuid();
        WebhookRegistradoEm = null;
        WebhookErro = null;
    }

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    public static string CalcularFinal(string chave)
    {
        var limpa = chave.Trim();
        return limpa.Length <= TamanhoFinal ? limpa : limpa[^TamanhoFinal..];
    }

    public static string RotuloAmbiente(AmbienteAsaas ambiente) => ambiente switch
    {
        AmbienteAsaas.Sandbox => "Sandbox (testes)",
        AmbienteAsaas.Producao => "Produção",
        _ => ambiente.ToString()
    };
}
