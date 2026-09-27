using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Plataforma;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Integracao;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Application.Mensageria;

/// <summary>
/// O retorno do canal na escolinha da mensagem (BL-M09-07): a entrega avança o log e a resposta fica registrada. Se o
/// gatilho espera resposta, o módulo de origem trata o botão (<see cref="IReceptorResposta"/>, a convocação no S5).
/// </summary>
public class ServicoRetornoMensagens(
    IMensageriaRepositorio repositorio,
    IEnumerable<IReceptorResposta> receptores,
    IRelogioBrasil relogio)
{
    public async Task<bool> AtualizarEntregaAsync(Guid mensagemId, RetornoEntrega retorno, DateTime em, string? detalhe,
        CancellationToken ct = default)
    {
        if (await repositorio.ObterMensagemAsync(mensagemId, ct) is not { } mensagem) return false;
        if (!mensagem.AtualizarEntrega(retorno, em, detalhe, relogio.Agora())) return false;
        await repositorio.SalvarAsync(ct);
        return true;
    }

    /// <returns>O que aconteceu com a resposta (vai para o webhook).</returns>
    public async Task<string> RegistrarRespostaAsync(Guid mensagemId, CanalMensagem canal, string? texto, string? payload, string? idExterno,
        DateTime em, CancellationToken ct = default)
    {
        if (await repositorio.ObterMensagemAsync(mensagemId, ct) is not { } mensagem) return "Mensagem não encontrada.";
        if (idExterno is not null && mensagem.Respostas.Any(r => r.IdExterno == idExterno)) return "Resposta já registrada.";

        // Botão de outra mensagem (a pessoa respondeu a uma convocação antiga) não vale para esta.
        if (payload is not null && (!PayloadBotao.TentarLer(payload, out var alvo, out _) || alvo != mensagem.Mensagem_ID))
            payload = null;

        var resposta = mensagem.RegistrarResposta(canal, texto, payload, idExterno, em, relogio.Agora());
        var regra = CatalogoGatilhos.Obter(mensagem.Gatilho);
        if (regra.EsperaResposta && payload is not null && receptores.FirstOrDefault(r => r.Gatilho == mensagem.Gatilho) is { } receptor)
        {
            var efeito = await receptor.ReceberAsync(mensagem, resposta, ct);
            resposta.Tratada = efeito is not null;
            resposta.Observacao = TextoHelper.Truncar(efeito ?? "O módulo de origem não mudou nada com esta resposta.", RespostaMensagem.TamanhoObservacao);
        }
        else
        {
            resposta.Observacao = regra.EsperaResposta
                ? "Texto livre: fica no log e não muda o estado (só os botões mudam)."
                : "Registrada no log: esta mensagem não espera resposta.";
        }
        await repositorio.SalvarAsync(ct);
        return resposta.Tratada ? "Resposta tratada." : "Resposta registrada.";
    }
}

/// <summary>
/// Webhook do WhatsApp (Cloud API), no padrão do DP: confere a assinatura, grava o corpo bruto e responde 200 na hora
/// (regra de ouro 6); o job interpreta. A escolinha sai do índice global (<see cref="RastreioWhatsApp"/>): o id da mensagem
/// (retorno de entrega e resposta a uma mensagem) ou, para texto solto, o telefone da última mensagem enviada a ele.
/// </summary>
public class ProcessadorWebhooksWhatsApp(
    IPlataformaRepositorio webhooks,
    IMensageriaRepositorio mensageria,
    IExecutorEscolinha executor,
    IAgendadorMensageria agendador,
    IRelogioBrasil relogio,
    OpcoesWhatsApp opcoes,
    ILogger<ProcessadorWebhooksWhatsApp> logger)
{
    public const string Origem = "whatsapp";

    /// <summary>Handshake GET da Meta: devolve o desafio se o token confere.</summary>
    public string? Verificar(string? modo, string? token, string? desafio) =>
        opcoes.Habilitado && modo == "subscribe" && !string.IsNullOrEmpty(token) && token == opcoes.TokenVerificacaoEfetivo ? desafio : null;

    public async Task<ResultadoRecebimentoWebhook> ReceberAsync(string corpo, string? assinatura, string? ip, CancellationToken ct = default)
    {
        if (!opcoes.Habilitado) return ResultadoRecebimentoWebhook.Desligado;
        var segredo = opcoes.SegredoAppEfetivo;
        if (string.IsNullOrWhiteSpace(segredo) || !AssinaturaWebhookWhatsApp.Valida(corpo, assinatura, segredo))
        {
            logger.LogWarning("Webhook do WhatsApp com assinatura inválida (IP {Ip}).", ip);
            return ResultadoRecebimentoWebhook.AssinaturaInvalida;
        }
        if (EventoWhatsApp.Ler(corpo) is null) return ResultadoRecebimentoWebhook.CorpoInvalido;

        var (novo, id) = await webhooks.GravarWebhookAsync(new WebhookRecebido
        {
            Origem = Origem,
            EventoExterno = AssinaturaWebhookWhatsApp.IdDoCorpo(corpo),
            Tipo = "whatsapp_business_account",
            Corpo = corpo,
            Ip = ip,
            RecebidoEm = relogio.Agora()
        }, ct);
        if (!novo) return ResultadoRecebimentoWebhook.Duplicado;

        agendador.ProcessarWebhookWhatsApp(id);
        return ResultadoRecebimentoWebhook.Aceito;
    }

    public async Task ProcessarAsync(Guid webhookId, CancellationToken ct = default)
    {
        if (await webhooks.ObterWebhookAsync(webhookId, ct) is not { Encerrado: false } webhook) return;
        if (EventoWhatsApp.Ler(webhook.Corpo) is not { } evento)
        {
            webhook.Ignorar(relogio.Agora(), "Corpo ilegível.");
            await webhooks.SalvarAsync(ct);
            return;
        }

        int retornos = 0, respostas = 0, semDono = 0;
        foreach (var status in evento.Status)
        {
            if (Retorno(status.Status) is not { } retorno) continue;
            if (await mensageria.RastreioPorIdExternoAsync(status.IdExterno, ct) is not { } rastreio)
            {
                semDono++;
                continue;
            }
            var em = Momento(status.EmUtc);
            if (await executor.ExecutarAsync(rastreio.Escolinha_ID, sp => sp.GetRequiredService<ServicoRetornoMensagens>()
                    .AtualizarEntregaAsync(rastreio.Mensagem_ID, retorno, em, status.Erro, ct), ct))
                retornos++;
        }

        foreach (var recebida in evento.Mensagens)
        {
            var rastreio = recebida.Contexto is { } contexto ? await mensageria.RastreioPorIdExternoAsync(contexto, ct) : null;
            rastreio ??= await mensageria.UltimoRastreioDoTelefoneAsync(Telefone.ChaveWhatsApp(recebida.De), ct);
            if (rastreio is null)
            {
                semDono++;
                continue;
            }
            var em = Momento(recebida.EmUtc);
            await executor.ExecutarAsync(rastreio.Escolinha_ID, sp => sp.GetRequiredService<ServicoRetornoMensagens>()
                .RegistrarRespostaAsync(rastreio.Mensagem_ID, CanalMensagem.WhatsApp, recebida.Texto ?? $"({recebida.Tipo})",
                    recebida.Payload, recebida.IdExterno, em, ct), ct);
            respostas++;
        }

        var resumo = $"{retornos} retorno(s) de entrega, {respostas} resposta(s)" + (semDono > 0 ? $", {semDono} sem mensagem nossa" : string.Empty) + ".";
        if (retornos == 0 && respostas == 0) webhook.Ignorar(relogio.Agora(), resumo);
        else webhook.MarcarProcessado(relogio.Agora(), resumo);
        await webhooks.SalvarAsync(ct);
    }

    private static RetornoEntrega? Retorno(string status) => status switch
    {
        "sent" => RetornoEntrega.Enviada,
        "delivered" => RetornoEntrega.Entregue,
        "read" => RetornoEntrega.Lida,
        "failed" => RetornoEntrega.Falhou,
        _ => null
    };

    private DateTime Momento(DateTime? utc) => utc is { } valor ? DataHoraBrasil.ConverterParaBrasilia(valor) : relogio.Agora();
}
