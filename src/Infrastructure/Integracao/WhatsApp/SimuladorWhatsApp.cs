using System.Text.Json;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Infrastructure.Integracao.WhatsApp;

/// <summary>
/// O WhatsApp de mentira do modo Simulado: guarda as mensagens num arquivo (sobrevive ao reinício do site) e deixa o
/// <c>/dev/whatsapp</c> fazer o papel do celular das famílias — entregar, ler, falhar, tocar num botão ou responder com
/// texto. Cada ação manda o webhook pelo endpoint de verdade, assinado: o caminho inteiro roda sem conta na Meta.
/// </summary>
public sealed class ArmazemSimuladorWhatsApp
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>O número "da escolinha" no simulador (quem manda as mensagens).</summary>
    public const string NumeroEmissor = "5543000000000";

    public string Arquivo { get; }

    public ArmazemSimuladorWhatsApp(string? arquivo = null)
    {
        Arquivo = arquivo ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MinhaEscolinha", "simulador-whatsapp.json");
    }

    public sealed class RespostaSimulada
    {
        public string Id { get; set; } = string.Empty;
        public string? Texto { get; set; }
        public string? Payload { get; set; }
        public DateTime EmUtc { get; set; }
    }

    public sealed class MensagemSimulada
    {
        public string Id { get; set; } = string.Empty;
        public Guid MensagemId { get; set; }
        public Guid EscolinhaId { get; set; }
        public string Telefone { get; set; } = string.Empty;
        public string Template { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;
        public List<string> Botoes { get; set; } = [];
        public string Status { get; set; } = "sent";
        public DateTime EnviadaEmUtc { get; set; }
        public List<RespostaSimulada> Respostas { get; set; } = [];
    }

    public Task<MensagemSimulada> RegistrarAsync(PedidoEnvioWhatsApp pedido, DateTime agoraUtc) => AlterarTodasAsync(mensagens =>
    {
        var nova = new MensagemSimulada
        {
            Id = $"wamid.SIM{Guid.NewGuid():N}",
            MensagemId = pedido.MensagemId,
            EscolinhaId = pedido.EscolinhaId,
            Telefone = pedido.Telefone,
            Template = pedido.Template,
            Texto = pedido.Texto,
            Botoes = pedido.Botoes.ToList(),
            EnviadaEmUtc = agoraUtc
        };
        mensagens.Add(nova);
        return nova;
    });

    public async Task<IReadOnlyList<MensagemSimulada>> ListarAsync() =>
        (await LerAsync()).OrderByDescending(m => m.EnviadaEmUtc).ToList();

    public async Task<MensagemSimulada?> ObterAsync(string id) => (await LerAsync()).FirstOrDefault(m => m.Id == id);

    public Task<MensagemSimulada?> AlterarAsync(string id, Action<MensagemSimulada> mudanca) => AlterarTodasAsync(mensagens =>
    {
        var mensagem = mensagens.FirstOrDefault(m => m.Id == id);
        if (mensagem is not null) mudanca(mensagem);
        return mensagem;
    });

    public Task LimparAsync() => AlterarTodasAsync(mensagens =>
    {
        mensagens.Clear();
        return 0;
    });

    /// <summary>
    /// O número como o WhatsApp devolve nas mensagens recebidas: no Brasil, o celular muitas vezes vem sem o 9º dígito
    /// (é o que o sistema precisa saber casar).
    /// </summary>
    public static string ComoOWhatsAppDevolve(string telefone) => Telefone.ChaveWhatsApp(telefone);

    /// <summary>Retorno de entrega, como a Cloud API manda (só o que o sistema lê).</summary>
    public static string MontarStatus(MensagemSimulada mensagem, string status, DateTimeOffset agora, string? erro = null) =>
        Envelope(new
        {
            messaging_product = "whatsapp",
            metadata = Metadados,
            statuses = new[]
            {
                new
                {
                    id = mensagem.Id,
                    status,
                    timestamp = agora.ToUnixTimeSeconds().ToString(),
                    recipient_id = ComoOWhatsAppDevolve(mensagem.Telefone),
                    errors = erro is null ? null : new[] { new { code = 131026, title = erro, error_data = new { details = erro } } }
                }
            }
        });

    /// <summary>A pessoa respondeu: botão (payload) ou texto. Com <paramref name="contexto"/>, é resposta àquela mensagem.</summary>
    public static string MontarResposta(string telefone, string idResposta, string? texto, string? payload, string? contexto,
        DateTimeOffset agora) =>
        Envelope(new
        {
            messaging_product = "whatsapp",
            metadata = Metadados,
            contacts = new[] { new { profile = new { name = "Família (simulador)" }, wa_id = ComoOWhatsAppDevolve(telefone) } },
            messages = new[]
            {
                new
                {
                    from = ComoOWhatsAppDevolve(telefone),
                    id = idResposta,
                    timestamp = agora.ToUnixTimeSeconds().ToString(),
                    type = payload is null ? "text" : "button",
                    text = payload is null ? new { body = texto } : null,
                    button = payload is null ? null : new { payload, text = texto },
                    context = contexto is null ? null : new { from = NumeroEmissor, id = contexto }
                }
            }
        });

    private static readonly object Metadados = new { display_phone_number = NumeroEmissor, phone_number_id = "SIMULADOR" };

    private static readonly JsonSerializerOptions SemNulos = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static string Envelope(object valor) => JsonSerializer.Serialize(new
    {
        @object = "whatsapp_business_account",
        entry = new[] { new { id = "WABA_SIMULADOR", changes = new[] { new { field = "messages", value = valor } } } }
    }, SemNulos);

    private async Task<T> AlterarTodasAsync<T>(Func<List<MensagemSimulada>, T> alteracao)
    {
        await Trava.WaitAsync();
        try
        {
            var mensagens = await LerSemTravaAsync();
            var resultado = alteracao(mensagens);
            Directory.CreateDirectory(Path.GetDirectoryName(Arquivo)!);
            await File.WriteAllTextAsync(Arquivo, JsonSerializer.Serialize(mensagens, Json));
            return resultado;
        }
        finally
        {
            Trava.Release();
        }
    }

    private async Task<List<MensagemSimulada>> LerAsync()
    {
        await Trava.WaitAsync();
        try
        {
            return await LerSemTravaAsync();
        }
        finally
        {
            Trava.Release();
        }
    }

    private async Task<List<MensagemSimulada>> LerSemTravaAsync()
    {
        if (!File.Exists(Arquivo)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<MensagemSimulada>>(await File.ReadAllTextAsync(Arquivo)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>O <see cref="IGatewayWhatsApp"/> do modo Simulado: a mensagem "chega" no celular do <c>/dev/whatsapp</c>.</summary>
public sealed class SimuladorWhatsApp(ArmazemSimuladorWhatsApp armazem, TimeProvider tempo) : IGatewayWhatsApp
{
    /// <summary>HttpClient nomeado que o <c>/dev/whatsapp</c> usa para mandar o webhook ao próprio sistema.</summary>
    public const string ClienteWebhook = "simulador-whatsapp-webhook";

    public async Task<EnvioWhatsApp> EnviarTemplateAsync(PedidoEnvioWhatsApp pedido, CancellationToken ct = default)
    {
        var mensagem = await armazem.RegistrarAsync(pedido, tempo.GetUtcNow().UtcDateTime);
        return new EnvioWhatsApp(mensagem.Id, ArmazemSimuladorWhatsApp.ComoOWhatsAppDevolve(pedido.Telefone));
    }
}
