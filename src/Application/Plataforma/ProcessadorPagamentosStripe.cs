using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Integracao.Stripe;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Integracao;
using MinhaEscolinha.Domain.Plataforma;

namespace MinhaEscolinha.Application.Plataforma;

public enum ResultadoRecebimentoWebhook
{
    Aceito,
    Duplicado,
    AssinaturaInvalida,
    CorpoInvalido,
    Desligado
}

/// <summary>
/// O lado de fora da requisição do pagamento: recebe o webhook do Stripe (grava o bruto e agenda), processa o evento,
/// e a varredura que cobre o webhook perdido. A decisão sobre a compra é sempre da <see cref="ConferenciaCompraCreditos"/>,
/// que relê a sessão no Stripe: o evento é só o aviso de que algo mudou.
/// </summary>
public class ProcessadorPagamentosStripe(
    IPlataformaRepositorio repositorio,
    IExecutorEscolinha executor,
    IAgendadorPlataforma agendador,
    IConsultasPlataforma consultas,
    IRelogioBrasil relogio,
    TimeProvider tempo,
    OpcoesStripe opcoes,
    ILogger<ProcessadorPagamentosStripe> logger)
{
    /// <summary>Chamado pelo endpoint anônimo. Confere a assinatura, grava o bruto e responde; o resto é job.</summary>
    public async Task<ResultadoRecebimentoWebhook> ReceberWebhookAsync(string corpo, string? assinatura, string? ip, CancellationToken ct = default)
    {
        if (!opcoes.Habilitado) return ResultadoRecebimentoWebhook.Desligado;

        var segredo = opcoes.SegredoWebhookEfetivo;
        if (string.IsNullOrWhiteSpace(segredo))
        {
            logger.LogWarning("Webhook do Stripe recusado: Stripe:SegredoWebhook não configurado (a varredura confere as compras).");
            return ResultadoRecebimentoWebhook.AssinaturaInvalida;
        }
        if (!AssinaturaWebhookStripe.Valida(corpo, assinatura, segredo, tempo.GetUtcNow()))
        {
            logger.LogWarning("Webhook do Stripe com assinatura inválida (IP {Ip}).", ip);
            return ResultadoRecebimentoWebhook.AssinaturaInvalida;
        }
        if (EventoStripe.Ler(corpo) is not { } evento) return ResultadoRecebimentoWebhook.CorpoInvalido;

        var (novo, id) = await repositorio.GravarWebhookAsync(new WebhookRecebido
        {
            Origem = WebhookRecebido.OrigemStripe,
            EventoExterno = evento.Id,
            Tipo = evento.Tipo,
            Corpo = corpo,
            Ip = ip,
            RecebidoEm = relogio.Agora()
        }, ct);
        if (!novo) return ResultadoRecebimentoWebhook.Duplicado;

        agendador.ProcessarWebhookStripe(id);
        return ResultadoRecebimentoWebhook.Aceito;
    }

    /// <summary>Job: interpreta o evento gravado. Falha retentável volta para a fila; o resto fica registrado no webhook.</summary>
    public async Task ProcessarWebhookAsync(Guid webhookId, CancellationToken ct = default)
    {
        if (await repositorio.ObterWebhookAsync(webhookId, ct) is not { Encerrado: false } webhook) return;

        var evento = EventoStripe.Ler(webhook.Corpo);
        Guid escolinhaId = Guid.Empty, compraId = Guid.Empty;
        var motivo = evento switch
        {
            null => "Corpo ilegível.",
            _ when !EventosStripe.Tratados.Contains(evento.Tipo) => $"Tipo {evento.Tipo} não é tratado.",
            _ when evento.Metadados.GetValueOrDefault(MetadadosStripe.Origem) != MetadadosStripe.ValorOrigem =>
                "Evento de outro uso da conta Stripe (sem a origem minha-escolinha).",
            _ when !Guid.TryParse(evento.Metadados.GetValueOrDefault(MetadadosStripe.Escolinha), out escolinhaId)
                   || !Guid.TryParse(evento.Metadados.GetValueOrDefault(MetadadosStripe.Compra), out compraId) =>
                "Evento sem a referência da escolinha e da compra.",
            _ => null
        };
        if (motivo is not null)
        {
            webhook.Ignorar(relogio.Agora(), motivo);
            await repositorio.SalvarAsync(ct);
            return;
        }

        try
        {
            var situacao = await ConferirCompraAsync(escolinhaId, compraId, evento!.ObjetoId, ct);
            if (situacao is null) webhook.Ignorar(relogio.Agora(), "Compra não encontrada nesta base.");
            else webhook.MarcarProcessado(relogio.Agora(), $"Compra {situacao}.");
        }
        catch (FalhaIntegracaoException ex) when (ex.Retentavel)
        {
            webhook.RegistrarTentativa(ex.Message);
            await repositorio.SalvarAsync(ct);
            throw;
        }
        catch (FalhaIntegracaoException ex)
        {
            logger.LogWarning("Webhook {Webhook} do Stripe não processado: {Mensagem}", webhookId, ex.Message);
            webhook.Falhar(relogio.Agora(), ex.Message);
        }
        await repositorio.SalvarAsync(ct);
    }

    /// <summary>
    /// Confere uma compra no escopo da escolinha dela. <c>null</c> = a compra não existe. O webhook e a volta do pagamento
    /// costumam chegar juntos: se a outra conferência gravou primeiro, a nossa foi desfeita inteira (versão de linha) e uma
    /// nova leitura, num escopo novo, encontra o desfecho dela, sem lançar créditos de novo.
    /// </summary>
    public async Task<SituacaoCompraCreditos?> ConferirCompraAsync(Guid escolinhaId, Guid compraId, string? sessaoDoEvento = null,
        CancellationToken ct = default)
    {
        try
        {
            return await ConferirNaEscolinhaAsync(escolinhaId, compraId, sessaoDoEvento, ct);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            logger.LogInformation("Compra {Compra} conferida ao mesmo tempo por outra operação: relendo.", compraId);
            return await ConferirNaEscolinhaAsync(escolinhaId, compraId, sessaoDoEvento, ct);
        }
    }

    private Task<SituacaoCompraCreditos?> ConferirNaEscolinhaAsync(Guid escolinhaId, Guid compraId, string? sessaoDoEvento,
        CancellationToken ct) =>
        executor.ExecutarAsync(escolinhaId,
            sp => sp.GetRequiredService<ConferenciaCompraCreditos>().ConferirAsync(compraId, sessaoDoEvento, ct), ct);

    /// <summary>
    /// Job de conferência. Falha retentável volta para a fila. Sessão que não existe mais no Stripe (404) encerra a compra
    /// como falha. As outras definitivas (chave errada, por exemplo) só vão para o log: nada muda na compra, e a varredura
    /// confere de novo depois que a configuração for corrigida.
    /// </summary>
    public async Task ConferirCompraPorJobAsync(Guid escolinhaId, Guid compraId, CancellationToken ct = default)
    {
        try
        {
            await ConferirCompraAsync(escolinhaId, compraId, null, ct);
        }
        catch (FalhaIntegracaoException ex) when (!ex.Retentavel)
        {
            logger.LogWarning("Conferência da compra {Compra} no Stripe falhou ({Codigo}): {Mensagem}", compraId, ex.CodigoHttp, ex.Message);
            if (ex.CodigoHttp != 404) return;
            await executor.ExecutarAsync(escolinhaId, async sp =>
            {
                var repositorioDaEscolinha = sp.GetRequiredService<IPlataformaRepositorio>();
                if (await repositorioDaEscolinha.ObterCompraAsync(compraId, ct) is { } compra
                    && compra.Falhar(relogio.Agora(), "A sessão de pagamento não existe mais no Stripe."))
                    await repositorioDaEscolinha.SalvarAsync(ct);
                return true;
            }, ct);
        }
    }

    /// <summary>Job recorrente: reenfileira a conferência de toda compra ainda sem desfecho (cobre o webhook perdido).</summary>
    public async Task<int> VarrerPendentesAsync(CancellationToken ct = default)
    {
        if (!opcoes.Habilitado) return 0;
        var total = 0;
        foreach (var escolinhaId in await consultas.IdsDasEscolinhasAsync(ct))
        {
            var pendentes = await executor.ExecutarAsync(escolinhaId,
                sp => sp.GetRequiredService<IPlataformaRepositorio>().ListarComprasPendentesAsync(ct), ct);
            foreach (var compraId in pendentes)
            {
                agendador.ConferirCompraCreditos(escolinhaId, compraId);
                total++;
            }
        }
        if (total > 0) logger.LogInformation("Varredura do Stripe: {Total} compra(s) pendente(s) enviadas para conferência.", total);
        return total;
    }
}

/// <summary>
/// Roda DENTRO da escolinha (via <see cref="IExecutorEscolinha"/>): relê a compra e a sessão no Stripe e aplica o
/// desfecho. Pagamento confirmado lança os créditos (movimento do razão + saldo) na mesma gravação da compra; a versão de
/// linha da compra e da conta impede que duas conferências simultâneas creditem duas vezes.
/// </summary>
public class ConferenciaCompraCreditos(
    IPlataformaRepositorio repositorio,
    IConfiguracaoRepositorio configuracao,
    IGatewayStripe gateway,
    IRelogioBrasil relogio,
    ILogger<ConferenciaCompraCreditos> logger)
{
    /// <summary>Sem sessão depois disso, a compra é dada como falha (o pagamento nunca chegou a abrir).</summary>
    public static readonly TimeSpan PrazoSemSessao = TimeSpan.FromHours(1);

    public async Task<SituacaoCompraCreditos?> ConferirAsync(Guid compraId, string? sessaoDoEvento, CancellationToken ct)
    {
        if (await repositorio.ObterCompraAsync(compraId, ct) is not { } compra) return null;
        if (compra.Situacao == SituacaoCompraCreditos.Paga) return compra.Situacao;

        var agora = relogio.Agora();
        var sessaoId = compra.SessaoCheckoutId ?? sessaoDoEvento;
        if (sessaoId is null)
        {
            if (agora - compra.IniciadaEm > PrazoSemSessao && compra.Falhar(agora, "O pagamento não chegou a ser aberto no Stripe."))
                await repositorio.SalvarAsync(ct);
            return compra.Situacao;
        }

        var sessao = await gateway.ConsultarSessaoAsync(sessaoId, ct);
        if (sessao.Metadados.TryGetValue(MetadadosStripe.Compra, out var referencia) && referencia != compraId.ToString())
        {
            logger.LogWarning("Sessão {Sessao} do Stripe é da compra {Referencia}, não da {Compra}: ignorada.", sessaoId, referencia, compraId);
            return compra.Situacao;
        }
        compra.SessaoCheckoutId ??= sessao.SessaoId;

        int? saldoAposCredito = null;
        if (sessao.Paga)
        {
            if (compra.Final) logger.LogWarning("Compra {Compra} estava {Situacao} e o Stripe confirmou o pagamento: créditos lançados.",
                compraId, compra.Situacao);
            if (compra.ConfirmarPagamento(agora, sessao.PagamentoId, sessao.MeioPagamento))
                saldoAposCredito = await LancarCreditosAsync(compra, agora, ct);
        }
        else if (sessao.Expirada) compra.Expirar(agora);
        else if (sessao.PagamentoRecusado) compra.Falhar(agora, "O boleto ou o Pix não foi pago.");
        else if (sessao.Concluida) compra.AguardarConfirmacao(sessao.MeioPagamento);

        await repositorio.SalvarAsync(ct);
        if (saldoAposCredito is { } saldo)
            logger.LogInformation("Compra {Compra} paga: {Creditos} crédito(s) lançados; saldo {Saldo}.", compraId, compra.Creditos, saldo);
        return compra.Situacao;
    }

    /// <summary>Movimento do razão + saldo, na mesma gravação da compra. Devolve o saldo depois do crédito.</summary>
    private async Task<int> LancarCreditosAsync(CompraCreditos compra, DateTime agora, CancellationToken ct)
    {
        var conta = await configuracao.ObterContaCreditosAsync(ct);
        if (conta is null)
        {
            conta = new ContaCreditos();
            configuracao.AdicionarContaCreditos(conta);
        }
        var movimento = conta.Lancar(TipoMovimentoCredito.Concessao, compra.Creditos,
            $"Compra do pacote {compra.NomePacote} (Stripe)", agora, compra.Autor_ID, compra.AutorNome)!;
        configuracao.AdicionarMovimentoCredito(movimento);
        return conta.Saldo;
    }
}
