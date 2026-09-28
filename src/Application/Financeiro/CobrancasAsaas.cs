using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Integracao.Asaas;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Application.Plataforma;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Financeiro;
using MinhaEscolinha.Domain.Integracao;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Application.Financeiro;

/// <summary>A conta Asaas da escolinha do contexto, com a chave aberta só para a chamada (nunca vai para o log).</summary>
public class ServicoAcessoAsaas(IConfiguracaoRepositorio configuracao, IProtetorSegredos protetor, ILogger<ServicoAcessoAsaas> logger)
{
    /// <returns><c>null</c> sem conta vinculada (ou com a chave ilegível: certificado de proteção perdido).</returns>
    public async Task<ContaAsaasAcesso?> AcessoAsync(CancellationToken ct = default)
    {
        if (await configuracao.ObterContaAsaasAsync(ct) is not { } conta) return null;
        if (protetor.Desproteger(conta.ChaveApiProtegida) is not { Length: > 0 } chave)
        {
            logger.LogError("A chave Asaas da escolinha {Escolinha} não pôde ser decifrada: vincule a conta de novo.", conta.Escolinha_ID);
            return null;
        }
        return new ContaAsaasAcesso(conta.Escolinha_ID, conta.Ambiente, chave, conta.Identificador);
    }

    /// <summary>O token que o webhook desta escolinha precisa trazer.</summary>
    /// <param name="criarSeFaltar">A conta vinculada antes do S4 ganha o token aqui (só em quem já grava: o cadastro do webhook e o simulador).</param>
    public async Task<string?> TokenWebhookAsync(bool criarSeFaltar = false, CancellationToken ct = default)
    {
        if (await configuracao.ObterContaAsaasAsync(ct) is not { } conta) return null;
        if (conta.TokenWebhookProtegido is { } protegido) return protetor.Desproteger(protegido);
        if (!criarSeFaltar) return null;

        var token = TokenWebhookAsaas.Gerar();
        conta.TokenWebhookProtegido = protetor.Proteger(token);
        await configuracao.SalvarAsync(ct);
        return token;
    }
}

/// <summary>
/// A emissão de uma cobrança na Asaas (BL-M06-02), num job, nunca no request (regra de ouro 2). Relê tudo antes de agir
/// (regra de ouro 4) e é reprocessável sem duplicar (spec §8.1): o cliente e a cobrança são procurados pela referência
/// externa antes de serem criados, então o retorno perdido de uma tentativa não vira segunda cobrança.
/// </summary>
public class EmissaoCobrancas(
    IFinanceiroRepositorio repositorio,
    ServicoAcessoAsaas contas,
    IGatewayAsaas gateway,
    ConciliacaoCobrancas conciliacao,
    IParametros parametros,
    IAgendadorFinanceiro agendador,
    OpcoesAsaas opcoes,
    IAmbienteExecucao ambiente,
    IRelogioBrasil relogio,
    ILogger<EmissaoCobrancas> logger)
{
    public const string MotivoSemConta = "Sem conta Asaas vinculada: nenhuma cobrança é emitida (Configuração › Conta de recebimento).";
    public const string MotivoSemPagador = "O aluno não tem pagador com CPF: marque o pagador na ficha e confira o CPF dele.";

    /// <returns>O que aconteceu (vai para o log do job).</returns>
    /// <exception cref="FalhaIntegracaoException">Retentável: o job tenta de novo (a cobrança continua prevista).</exception>
    public async Task<string> EmitirAsync(Guid cobrancaId, CancellationToken ct = default)
    {
        if (await repositorio.ObterAsync(cobrancaId, ct) is not { Situacao: SituacaoCobranca.Prevista } cobranca) return "Nada a emitir.";
        if (!opcoes.Habilitado) return "A integração com a Asaas está desligada nesta instalação.";

        var p = (await parametros.ObterAsync(ct)).Financeiro;
        var hoje = DateOnly.FromDateTime(relogio.Hoje());
        if (!cobranca.ProntaParaEmitir(hoje, p.EmissaoDiasAntesVencimento)) return "Ainda não é hora de emitir (ou espera confirmação).";

        if (await contas.AcessoAsync(ct) is not { } conta) return MotivoSemConta;
        if (conta.Ambiente == AmbienteAsaas.Producao && !ambiente.EhProducao)
            return "Conta Asaas de produção fora do ambiente de produção: nada é emitido.";
        if (await repositorio.PagadorAsync(cobranca.Aluno_ID, ct) is not { } pagador || !Cpf.EhValido(Cpf.Normalizar(pagador.Cpf)))
            return MotivoSemPagador;

        var vencimentoAnterior = cobranca.PrepararEmissao(pagador.ResponsavelId, pagador.Nome, p.MultaPercentual, p.JurosPercentualMes, hoje);
        if (vencimentoAnterior is { } antes)
            logger.LogInformation("Cobrança {Cobranca}: o vencimento {Antes:dd/MM/yyyy} já passou; emitida com vencimento hoje.", cobranca.Cobranca_ID, antes);

        try
        {
            var clienteId = await ClienteAsync(conta, pagador, ct);
            var existente = await gateway.BuscarCobrancaAsync(conta, cobranca.Cobranca_ID, ct);
            var pagamento = existente ?? await gateway.CriarCobrancaAsync(conta, new CobrancaAsaasPedido(cobranca.Cobranca_ID, clienteId,
                cobranca.Valor, cobranca.Vencimento, await DescricaoAsync(cobranca, ct), cobranca.MultaPercentual!.Value,
                cobranca.JurosPercentualMes!.Value), ct);

            cobranca.RegistrarEmissao(pagamento.Id, pagamento.UrlFatura, conta.Identificador, relogio.Agora());
            if (existente is not null && (existente.Recebida || existente.Excluida)) await conciliacao.AplicarAsync(cobranca, existente, null, ct);
            await repositorio.SalvarAsync(ct);
            // O Pix do portal num job à parte: a falha dele não refaz a emissão.
            if (cobranca.EmAberto) agendador.ObterPix(conta.EscolinhaId, cobranca.Cobranca_ID);
            logger.LogInformation("Cobrança {Cobranca} emitida na Asaas ({IdAsaas}{Reencontrada}).", cobranca.Cobranca_ID, pagamento.Id,
                existente is null ? string.Empty : ", reencontrada pela referência");
            return $"Emitida na Asaas ({pagamento.Id}).";
        }
        catch (FalhaIntegracaoException ex) when (ex.Retentavel)
        {
            cobranca.RegistrarFalhaEmissao(ex.Message, definitiva: false);
            await repositorio.SalvarAsync(ct);
            throw;
        }
        catch (FalhaIntegracaoException ex)
        {
            cobranca.RegistrarFalhaEmissao(ex.Message, definitiva: true);
            await repositorio.SalvarAsync(ct);
            logger.LogWarning("A Asaas recusou a cobrança {Cobranca}: {Erro}", cobranca.Cobranca_ID, ex.Message);
            return $"Recusada pela Asaas: {ex.Message}";
        }
    }

    private async Task<string> ClienteAsync(ContaAsaasAcesso conta, PagadorDoAluno pagador, CancellationToken ct)
    {
        if (await repositorio.ClienteAsaasAsync(pagador.ResponsavelId, conta.Identificador, ct) is { } conhecido) return conhecido.IdAsaas;
        var id = await gateway.BuscarClienteAsync(conta, pagador.ResponsavelId, ct)
                 ?? await gateway.CriarClienteAsync(conta, new ClienteAsaasPedido(pagador.ResponsavelId, pagador.Nome,
                     Cpf.Normalizar(pagador.Cpf)!, pagador.Email, pagador.Telefone), ct);
        repositorio.AdicionarCliente(new ClienteAsaas
        {
            ClienteAsaas_ID = Guid.NewGuid(),
            Responsavel_ID = pagador.ResponsavelId,
            Conta = conta.Identificador,
            IdAsaas = id,
            CriadoEm = relogio.Agora()
        });
        return id;
    }

    /// <summary>"Mensalidade outubro/2026 · Ana · Sub-11": a família sabe de quem é a fatura.</summary>
    private async Task<string> DescricaoAsync(Cobranca cobranca, CancellationToken ct)
    {
        var aluno = EmissorGatilhos.PrimeiroNome(await repositorio.NomeDoAlunoAsync(cobranca.Aluno_ID, ct));
        var turma = (await repositorio.TurmasAsync(ct)).FirstOrDefault(t => t.Id == cobranca.Turma_ID);
        // P44: a turma cobrada por período diz o período na fatura, a partir do mês da cobrança.
        var titulo = turma is { PeriodicidadeMeses: > 1 } periodo
            ? $"Cobrança {Domain.Turmas.Turma.RotuloPeriodicidade(periodo.PeriodicidadeMeses).ToLowerInvariant()} a partir de"
            : "Mensalidade";
        var texto = $"{titulo} {Cobranca.RotuloCompetencia(cobranca.Competencia)} · {aluno}" + (turma is null ? string.Empty : $" · {turma.Nome}");
        return TextoHelper.Truncar(texto, 450) ?? texto;
    }
}

/// <summary>
/// O Pix copia e cola da mensalidade em aberto (BL-M10-06): buscado na Asaas por um job, depois da emissão e na rotina
/// diária (quando falta ou venceu), e guardado com a validade, para o portal mostrar sem chamar a Asaas no request (regra
/// de ouro 2). Sem Pix, o portal oferece a fatura, que também tem o Pix.
/// </summary>
public class PixCobrancas(
    IFinanceiroRepositorio repositorio,
    ServicoAcessoAsaas contas,
    IGatewayAsaas gateway,
    OpcoesAsaas opcoes,
    IRelogioBrasil relogio,
    ILogger<PixCobrancas> logger)
{
    /// <returns>O que aconteceu (vai para o log do job).</returns>
    /// <exception cref="FalhaIntegracaoException">Retentável: o job tenta de novo.</exception>
    public async Task<string> AtualizarAsync(Guid cobrancaId, CancellationToken ct = default)
    {
        if (await repositorio.ObterAsync(cobrancaId, ct) is not { IdAsaas: { } idAsaas } cobranca || !cobranca.EmAberto || cobranca.ComSinal)
            return "Nada a buscar: a mensalidade não está em aberto na Asaas.";
        if (cobranca.PixValidoEm(relogio.Agora())) return "O Pix já está guardado.";
        if (!opcoes.Habilitado) return "A integração com a Asaas está desligada nesta instalação.";
        if (await contas.AcessoAsync(ct) is not { } conta || cobranca.ContaAsaas != conta.Identificador)
            return "A mensalidade é de outra conta Asaas: sem Pix pelo portal.";

        try
        {
            if (await gateway.ObterPixAsync(conta, idAsaas, ct) is not { } pix) return "A Asaas não tem Pix para esta cobrança.";
            if (pix.Imagem is not null && !Cobranca.ImagemValida(pix.Imagem))
                logger.LogWarning("QR code do Pix da cobrança {Cobranca} não guardado: {Tamanho} caracteres ou fora do base64 (o máximo é {Maximo}).",
                    cobrancaId, pix.Imagem.Length, Cobranca.TamanhoPixImagem);
            if (!cobranca.RegistrarPix(pix.CopiaECola, pix.ExpiraEm, pix.Imagem))
            {
                logger.LogWarning("Pix da cobrança {Cobranca} não guardado: {Tamanho} caracteres (o máximo é {Maximo}).", cobrancaId,
                    pix.CopiaECola.Length, Cobranca.TamanhoPix);
                return "Pix grande demais: não guardado (a fatura continua com o Pix).";
            }
            await repositorio.SalvarAsync(ct);
            return "Pix guardado.";
        }
        catch (FalhaIntegracaoException ex) when (!ex.Retentavel)
        {
            logger.LogWarning("A Asaas recusou o Pix da cobrança {Cobranca}: {Erro}", cobrancaId, ex.Message);
            return $"Sem Pix: {ex.Message}";
        }
    }
}

/// <summary>
/// A conciliação (BL-M06-05): aplica na cobrança o que a Asaas diz dela, venha do webhook, da varredura ou da revalidação
/// do envio. Pagamento com o valor original conciliado muda a situação na hora, sem ação humana (CA-7), para a régua
/// (as mensagens que ainda não saíram são recolhidas, regra de ouro 5) e agradece a família. Valor diferente, estorno,
/// contestação e exclusão por fora só sinalizam (CA-9, P18). Receber o mesmo evento duas vezes não tem segundo efeito.
/// </summary>
public class ConciliacaoCobrancas(
    IFinanceiroRepositorio repositorio,
    EmissorGatilhos emissor,
    IRelogioBrasil relogio,
    ILogger<ConciliacaoCobrancas> logger)
{
    public const string MotivoReguaParou = "Pagamento confirmado: a régua da mensalidade parou.";
    public const string MotivoDivergencia = "Pagamento com divergência: a régua espera a gestão resolver.";

    /// <summary>O webhook de uma cobrança: acha a nossa pela referência externa (ou pelo id da Asaas), aplica e salva.</summary>
    /// <returns>O que aconteceu, ou <c>null</c> se a cobrança não é nossa.</returns>
    public async Task<string?> AplicarDoWebhookAsync(EventoAsaas evento, CancellationToken ct = default)
    {
        if (evento.Pagamento is not { } pagamento) return null;
        var cobranca = pagamento.CobrancaId is { } id ? await repositorio.ObterAsync(id, ct) : null;
        cobranca ??= await repositorio.ObterPorIdAsaasAsync(pagamento.Id, ct);
        if (cobranca is null) return null;

        var resultado = await AplicarAsync(cobranca, pagamento, evento.Tipo, ct);
        await repositorio.SalvarAsync(ct);
        return $"{evento.Tipo}: {resultado}";
    }

    /// <summary>Aplica sem salvar (quem chama salva, na mesma gravação das mensagens).</summary>
    public async Task<string> AplicarAsync(Cobranca cobranca, PagamentoAsaas pagamento, string? evento, CancellationToken ct = default)
    {
        var agora = relogio.Agora();
        var hoje = DateOnly.FromDateTime(agora);
        cobranca.MarcarConferida(agora);

        if (pagamento.Recebida)
        {
            var resultado = cobranca.RegistrarPagamento(pagamento.ValorDeReferencia, pagamento.Valor, pagamento.PagaEm ?? hoje,
                pagamento.MeioDoPagamento, agora);
            switch (resultado)
            {
                case ResultadoPagamento.Conciliada:
                    await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemCobranca, cobranca.Cobranca_ID, MotivoReguaParou, ct);
                    await emissor.PagamentoConfirmadoAsync(cobranca, await repositorio.NomeDoAlunoAsync(cobranca.Aluno_ID, ct), ct);
                    logger.LogInformation("Cobrança {Cobranca} conciliada ({Situacao}).", cobranca.Cobranca_ID, cobranca.Situacao);
                    return $"conciliada: {Cobranca.Rotulo(cobranca.Situacao).ToLowerInvariant()}";
                case ResultadoPagamento.Divergente:
                case ResultadoPagamento.DepoisDeEncerrada:
                    await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemCobranca, cobranca.Cobranca_ID, MotivoDivergencia, ct);
                    logger.LogWarning("Cobrança {Cobranca} sinalizada: {Sinal}.", cobranca.Cobranca_ID, cobranca.Sinal);
                    return $"sinalizada: {Cobranca.Rotulo(cobranca.Sinal).ToLowerInvariant()}";
                default:
                    return "já estava paga";
            }
        }

        if (StatusAsaas.Estorno(pagamento.Status) && cobranca.Quitada)
        {
            cobranca.Sinalizar(SinalCobranca.Estorno, $"A Asaas registra o estorno ({pagamento.Status}) de um pagamento conciliado.", agora);
            return "sinalizada: estorno";
        }

        if (StatusAsaas.Chargeback(pagamento.Status))
        {
            cobranca.Sinalizar(SinalCobranca.Chargeback, $"Contestação do pagamento no cartão ({pagamento.Status}).", agora);
            return "sinalizada: contestação";
        }

        if ((pagamento.Excluida || evento == EventoAsaas.PagamentoExcluido) && cobranca.EmAberto)
        {
            cobranca.Sinalizar(SinalCobranca.ExcluidaNaAsaas, "A cobrança foi excluída na Asaas, por fora do sistema: a fatura não vale mais.", agora);
            await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemCobranca, cobranca.Cobranca_ID, MotivoDivergencia, ct);
            return "sinalizada: excluída na Asaas";
        }

        if (pagamento.Status == StatusAsaas.Vencida && cobranca.Vencer(hoje)) return "vencida";
        return "sem mudança";
    }
}

/// <summary>
/// Webhook da Asaas, no padrão do DP: confere o token da escolinha (cabeçalho <c>asaas-access-token</c>), grava o corpo
/// bruto e responde 200 na hora (regra de ouro 6); o job interpreta. A escolinha vem do endereço (cada conta avisa no
/// endereço da própria escolinha) e fica gravada com o corpo. O evento repetido não tem efeito (único por escolinha e id).
/// </summary>
public class ProcessadorWebhooksAsaas(
    IPlataformaRepositorio webhooks,
    IExecutorEscolinha executor,
    IAgendadorFinanceiro agendador,
    IRelogioBrasil relogio,
    OpcoesAsaas opcoes,
    ILogger<ProcessadorWebhooksAsaas> logger)
{
    public async Task<ResultadoRecebimentoWebhook> ReceberAsync(Guid escolinhaId, string corpo, string? token, string? ip,
        CancellationToken ct = default)
    {
        if (!opcoes.Habilitado) return ResultadoRecebimentoWebhook.Desligado;

        string? esperado;
        try
        {
            esperado = await executor.ExecutarAsync(escolinhaId,
                sp => sp.GetRequiredService<ServicoAcessoAsaas>().TokenWebhookAsync(criarSeFaltar: false, ct), ct);
        }
        catch (InvalidOperationException)
        {
            esperado = null;
        }
        if (!TokenWebhookAsaas.Confere(token, esperado))
        {
            logger.LogWarning("Webhook da Asaas com token inválido para a escolinha {Escolinha} (IP {Ip}).", escolinhaId, ip);
            return ResultadoRecebimentoWebhook.AssinaturaInvalida;
        }
        if (EventoAsaas.Ler(corpo) is not { } evento) return ResultadoRecebimentoWebhook.CorpoInvalido;

        var (novo, id) = await webhooks.GravarWebhookAsync(new WebhookRecebido
        {
            Origem = WebhookRecebido.OrigemAsaas,
            Escolinha_ID = escolinhaId,
            EventoExterno = $"{escolinhaId:N}:{evento.Id}",
            Tipo = evento.Tipo,
            Corpo = corpo,
            Ip = ip,
            RecebidoEm = relogio.Agora()
        }, ct);
        if (!novo) return ResultadoRecebimentoWebhook.Duplicado;

        agendador.ProcessarWebhookAsaas(id);
        return ResultadoRecebimentoWebhook.Aceito;
    }

    public async Task ProcessarAsync(Guid webhookId, CancellationToken ct = default)
    {
        if (await webhooks.ObterWebhookAsync(webhookId, ct) is not { Encerrado: false } webhook) return;
        if (EventoAsaas.Ler(webhook.Corpo) is not { } evento || webhook.Escolinha_ID is not { } escolinhaId)
        {
            webhook.Ignorar(relogio.Agora(), "Corpo ilegível ou sem escolinha.");
            await webhooks.SalvarAsync(ct);
            return;
        }
        if (evento.Pagamento is null)
        {
            webhook.Ignorar(relogio.Agora(), $"{evento.Tipo}: evento sem cobrança (a Minha Escolinha só trata cobranças).");
            await webhooks.SalvarAsync(ct);
            return;
        }

        var resultado = await executor.ExecutarAsync(escolinhaId,
            sp => sp.GetRequiredService<ConciliacaoCobrancas>().AplicarDoWebhookAsync(evento, ct), ct);
        if (resultado is null) webhook.Ignorar(relogio.Agora(), $"{evento.Tipo}: cobrança que não foi emitida pela Minha Escolinha.");
        else webhook.MarcarProcessado(relogio.Agora(), resultado);
        await webhooks.SalvarAsync(ct);
    }
}

/// <summary>
/// A varredura (regra de ouro 4, padrão do DP): relê na Asaas as cobranças em aberto menos conferidas, para cobrir o webhook
/// que não chegou (conta sem webhook, fila da Asaas interrompida). A cobrança de uma conta anterior fica sinalizada.
/// </summary>
public class VarreduraCobrancas(
    IFinanceiroRepositorio repositorio,
    ServicoAcessoAsaas contas,
    IGatewayAsaas gateway,
    ConciliacaoCobrancas conciliacao,
    OpcoesAsaas opcoes,
    IRelogioBrasil relogio,
    ILogger<VarreduraCobrancas> logger)
{
    public const int Lote = 50;
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(50);

    public async Task<int> VarrerAsync(CancellationToken ct = default)
    {
        if (!opcoes.Habilitado || await contas.AcessoAsync(ct) is not { } conta) return 0;
        var conferidas = 0;
        foreach (var cobranca in await repositorio.ParaConferirAsync(relogio.Agora().Subtract(Intervalo), Lote, ct))
        {
            if (cobranca.ContaAsaas != conta.Identificador)
            {
                if (cobranca.Sinal != SinalCobranca.ContaAnterior)
                    cobranca.Sinalizar(SinalCobranca.ContaAnterior,
                        "Emitida na conta Asaas anterior à troca: confira na conta antiga se foi paga.", relogio.Agora());
                cobranca.MarcarConferida(relogio.Agora());
                await repositorio.SalvarAsync(ct);
                continue;
            }

            try
            {
                var pagamento = await gateway.ObterCobrancaAsync(conta, cobranca.IdAsaas!, ct)
                                ?? new PagamentoAsaas(cobranca.IdAsaas!, "DELETED", cobranca.Valor, null, cobranca.Vencimento, null, null,
                                    cobranca.Cobranca_ID.ToString(), true, cobranca.UrlFatura);
                await conciliacao.AplicarAsync(cobranca, pagamento, null, ct);
                await repositorio.SalvarAsync(ct);
                conferidas++;
            }
            catch (FalhaIntegracaoException ex)
            {
                logger.LogWarning("Varredura: a cobrança {Cobranca} não foi conferida na Asaas ({Erro}).", cobranca.Cobranca_ID, ex.Message);
                if (ex.Retentavel) break;
            }
        }
        return conferidas;
    }

    /// <summary>Relê uma cobrança (pedido do envio que viu a cobrança paga na Asaas, ou do simulador).</summary>
    public async Task<string> ConferirAsync(Guid cobrancaId, CancellationToken ct = default)
    {
        if (await repositorio.ObterAsync(cobrancaId, ct) is not { IdAsaas: { } idAsaas } cobranca) return "Cobrança sem emissão.";
        if (!opcoes.Habilitado || await contas.AcessoAsync(ct) is not { } conta || conta.Identificador != cobranca.ContaAsaas)
            return "Sem a conta da cobrança.";
        var pagamento = await gateway.ObterCobrancaAsync(conta, idAsaas, ct);
        if (pagamento is null) return "A cobrança não existe na Asaas.";
        var resultado = await conciliacao.AplicarAsync(cobranca, pagamento, null, ct);
        await repositorio.SalvarAsync(ct);
        return resultado;
    }
}

/// <summary>
/// A régua não cobra quem pagou (CA-8, spec §8.1 "em nenhuma hipótese"). Na hora do envio da mensalidade a vencer ou
/// vencida: a cobrança precisa estar em aberto aqui, sem divergência, e em aberto também na Asaas — o pagamento pode ter
/// entrado e o webhook ainda não ter chegado. Asaas fora do ar: a mensagem espera (a falha retentável volta à fila).
/// </summary>
public class ValidadorOrigemCobranca(
    IFinanceiroRepositorio repositorio,
    ServicoAcessoAsaas contas,
    IGatewayAsaas gateway,
    IAgendadorFinanceiro agendador,
    OpcoesAsaas opcoes) : IValidadorOrigemMensagem
{
    public string TipoOrigem => EmissorGatilhos.OrigemCobranca;

    public async Task<string?> MotivoParaNaoEnviarAsync(Mensagem mensagem, CancellationToken ct = default)
    {
        if (mensagem.Gatilho == GatilhoMensagem.PagamentoConfirmado) return null;
        if (await repositorio.ObterAsync(mensagem.Origem_ID, ct) is not { } cobranca) return "A cobrança não existe mais.";
        if (!cobranca.EmAberto) return $"A mensalidade está {Cobranca.Rotulo(cobranca.Situacao).ToLowerInvariant()}: nada a cobrar.";
        if (cobranca.ComSinal) return "A cobrança tem divergência em tratamento: a régua espera a gestão.";

        if (!opcoes.Habilitado || cobranca.IdAsaas is not { } idAsaas) return null;
        if (await contas.AcessoAsync(ct) is not { } conta || conta.Identificador != cobranca.ContaAsaas)
            return "A cobrança está numa conta Asaas anterior: ninguém é cobrado até a gestão conferir.";

        var pagamento = await gateway.ObterCobrancaAsync(conta, idAsaas, ct);
        if (pagamento is null || pagamento.Excluida)
        {
            agendador.Conferir(mensagem.Escolinha_ID, cobranca.Cobranca_ID);
            return "A cobrança não está mais na Asaas: a gestão confere.";
        }
        if (pagamento.Recebida || StatusAsaas.Estorno(pagamento.Status) || StatusAsaas.Chargeback(pagamento.Status))
        {
            agendador.Conferir(mensagem.Escolinha_ID, cobranca.Cobranca_ID);
            return "A Asaas já registra o pagamento desta mensalidade: a conciliação foi pedida.";
        }
        return null;
    }
}

/// <summary>Os trabalhos avulsos na Asaas: excluir a cobrança cancelada ou isenta, registrar o recebimento por fora e o webhook.</summary>
public class OperacoesAsaas(
    IFinanceiroRepositorio repositorio,
    Alunos.IAlunosRepositorio alunos,
    IConfiguracaoRepositorio configuracao,
    ServicoAcessoAsaas contas,
    IGatewayAsaas gateway,
    OpcoesAsaas opcoes,
    IRelogioBrasil relogio,
    ILogger<OperacoesAsaas> logger)
{
    public async Task<string> ExcluirAsync(Guid cobrancaId, CancellationToken ct = default)
    {
        if (await repositorio.ObterAsync(cobrancaId, ct) is not { IdAsaas: { } idAsaas, Encerrada: true } cobranca) return "Nada a excluir.";
        if (!opcoes.Habilitado || await contas.AcessoAsync(ct) is not { } conta || conta.Identificador != cobranca.ContaAsaas)
            return "Sem a conta da cobrança: exclua na Asaas.";
        await gateway.ExcluirCobrancaAsync(conta, idAsaas, ct);
        logger.LogInformation("Cobrança {Cobranca} ({IdAsaas}) excluída na Asaas ({Situacao}).", cobrancaId, idAsaas, cobranca.Situacao);
        return "Excluída na Asaas.";
    }

    /// <summary>
    /// P42: o cliente do responsável na conta da escolinha recebe o cadastro de agora (nome, CPF, e-mail e celular). Sem
    /// cliente ainda, nada a fazer: ele nasce na próxima emissão, já com os dados novos. A recusa definitiva fica no log.
    /// </summary>
    public async Task<string> AtualizarClienteAsync(Guid responsavelId, CancellationToken ct = default)
    {
        if (!opcoes.Habilitado || await contas.AcessoAsync(ct) is not { } conta) return "Sem conta Asaas ligada: nada a atualizar.";
        if (await repositorio.ClienteAsaasAsync(responsavelId, conta.Identificador, ct) is not { } cliente)
            return "O responsável ainda não é cliente na Asaas: nasce na próxima emissão, com os dados novos.";
        if (await alunos.ObterResponsavelAsync(responsavelId, ct) is not { } responsavel) return "Responsável não encontrado.";
        try
        {
            await gateway.AtualizarClienteAsync(conta, cliente.IdAsaas,
                new ClienteAsaasPedido(responsavelId, responsavel.Nome, Cpf.Normalizar(responsavel.Cpf) ?? string.Empty, responsavel.Email,
                    responsavel.Telefone), ct);
            return "Cliente atualizado na Asaas.";
        }
        catch (FalhaIntegracaoException ex) when (!ex.Retentavel)
        {
            logger.LogWarning("A Asaas recusou a atualização do cliente {Cliente} (responsável {Responsavel}): {Erro}", cliente.IdAsaas,
                responsavelId, ex.Message);
            return $"Recusada pela Asaas: {ex.Message}";
        }
    }

    public async Task<string> ReceberEmDinheiroAsync(Guid cobrancaId, CancellationToken ct = default)
    {
        if (await repositorio.ObterAsync(cobrancaId, ct) is not { IdAsaas: { } idAsaas, Quitada: true, PagaEm: { } data } cobranca)
            return "Nada a registrar.";
        if (!opcoes.Habilitado || await contas.AcessoAsync(ct) is not { } conta || conta.Identificador != cobranca.ContaAsaas)
            return "Sem a conta da cobrança: registre na Asaas.";
        await gateway.ReceberEmDinheiroAsync(conta, idAsaas, data, cobranca.ValorPago ?? cobranca.Valor, ct);
        return "Recebimento registrado na Asaas.";
    }

    /// <summary>Cadastra o webhook de cobranças na conta da escolinha (modo Api), com o endereço dela e o token.</summary>
    public async Task<string> RegistrarWebhookAsync(CancellationToken ct = default)
    {
        if (!opcoes.Api || await configuracao.ObterContaAsaasAsync(ct) is not { } registro) return "Nada a registrar.";
        if (await contas.AcessoAsync(ct) is not { } conta || await contas.TokenWebhookAsync(criarSeFaltar: true, ct) is not { } token)
            return "Sem conta ou sem token.";
        try
        {
            await gateway.RegistrarWebhookAsync(conta, opcoes.UrlWebhook(conta.EscolinhaId), token,
                string.IsNullOrWhiteSpace(opcoes.EmailAvisosWebhook) ? null : opcoes.EmailAvisosWebhook, ct);
            registro.WebhookRegistradoEm = relogio.Agora();
            registro.WebhookErro = null;
            await configuracao.SalvarAsync(ct);
            return "Webhook cadastrado na Asaas.";
        }
        catch (FalhaIntegracaoException ex) when (!ex.Retentavel)
        {
            registro.WebhookErro = TextoHelper.Truncar(ex.Message, ContaAsaasEscolinha.TamanhoErroWebhook);
            await configuracao.SalvarAsync(ct);
            logger.LogWarning("O webhook não foi cadastrado na conta Asaas da escolinha {Escolinha}: {Erro}", conta.EscolinhaId, ex.Message);
            return $"Recusado: {ex.Message}";
        }
    }
}
