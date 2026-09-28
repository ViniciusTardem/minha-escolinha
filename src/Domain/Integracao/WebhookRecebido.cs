using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Integracao;

public enum SituacaoWebhook
{
    Recebido = 10,
    Processado = 20,

    /// <summary>
    /// Evento que não é nosso ou que não pede ação (o motivo fica na observação). A linha nunca é apagada; o corpo sai
    /// depois de <see cref="WebhookRecebido.DiasDoCorpo"/> dias, como o dos outros encerrados (P22).
    /// </summary>
    Ignorado = 30,

    /// <summary>Falha definitiva no processamento (a retentável volta para a fila).</summary>
    Falhou = 40
}

/// <summary>
/// Chamada de webhook gravada BRUTA antes de qualquer interpretação e respondida com 200 na hora (regra de ouro 6):
/// um erro nosso de leitura não perde o evento, dá para reprocessar a partir do bruto. Global: a escolinha sai do
/// conteúdo, no processamento. Único por (origem, evento): o reenvio do mesmo evento não tem efeito.
/// Serve ao Stripe, à Asaas e ao WhatsApp. O corpo do encerrado sai depois de <see cref="DiasDoCorpo"/> dias (P22).
/// </summary>
public class WebhookRecebido
{
    public const string OrigemStripe = "stripe";
    public const string OrigemAsaas = "asaas";

    /// <summary>
    /// Por quantos dias o corpo bruto do webhook encerrado fica guardado (P22): o bastante para reprocessar e investigar.
    /// Depois, só a linha fica: o SQL Express tem teto de 10 GB, e o corpo traz celular e nome das famílias.
    /// </summary>
    public const int DiasDoCorpo = 90;

    public const int TamanhoOrigem = 20;
    public const int TamanhoEvento = 255;
    public const int TamanhoTipo = 100;
    public const int TamanhoObservacao = 1000;
    public const int TamanhoIp = 45;

    public Guid WebhookRecebido_ID { get; set; }
    public string Origem { get; set; } = string.Empty;

    /// <summary>
    /// A escolinha que o endereço do webhook indicou (Asaas: cada conta avisa no endereço da própria escolinha, e o token
    /// dela já foi conferido). Vazio quando a escolinha sai do conteúdo (Stripe, WhatsApp).
    /// </summary>
    public Guid? Escolinha_ID { get; set; }

    /// <summary>Id do evento no parceiro (<c>evt_...</c> no Stripe).</summary>
    public string EventoExterno { get; set; } = string.Empty;

    public string? Tipo { get; set; }
    public string Corpo { get; set; } = string.Empty;
    public string? Ip { get; set; }
    public DateTime RecebidoEm { get; set; }

    public SituacaoWebhook Situacao { get; private set; } = SituacaoWebhook.Recebido;
    public DateTime? ProcessadoEm { get; private set; }
    public int Tentativas { get; private set; }
    public string? Observacao { get; private set; }

    /// <summary>Quando o corpo bruto foi apagado pela guarda de <see cref="DiasDoCorpo"/> dias; a linha fica, sem o conteúdo.</summary>
    public DateTime? CorpoApagadoEm { get; private set; }

    public bool Encerrado => Situacao is SituacaoWebhook.Processado or SituacaoWebhook.Ignorado or SituacaoWebhook.Falhou;

    public void MarcarProcessado(DateTime agora, string? observacao = null)
    {
        Situacao = SituacaoWebhook.Processado;
        ProcessadoEm = agora;
        Tentativas++;
        Observacao = TextoHelper.Truncar(observacao, TamanhoObservacao);
    }

    public void Ignorar(DateTime agora, string motivo)
    {
        Situacao = SituacaoWebhook.Ignorado;
        ProcessadoEm = agora;
        Tentativas++;
        Observacao = TextoHelper.Truncar(motivo, TamanhoObservacao);
    }

    /// <summary>Falha retentável: continua Recebido (a fila tenta de novo), com a última mensagem.</summary>
    public void RegistrarTentativa(string erro)
    {
        Tentativas++;
        Observacao = TextoHelper.Truncar(erro, TamanhoObservacao);
    }

    public void Falhar(DateTime agora, string erro)
    {
        Situacao = SituacaoWebhook.Falhou;
        ProcessadoEm = agora;
        Tentativas++;
        Observacao = TextoHelper.Truncar(erro, TamanhoObservacao);
    }
}
