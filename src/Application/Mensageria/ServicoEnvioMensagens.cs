using System.Text.Json;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Application.Mensageria;

/// <summary>
/// Confere, antes do envio, se o registro que originou a mensagem ainda justifica mandá-la (plano §3.7: revalidar a
/// origem na hora do envio). Um por tipo de origem.
/// </summary>
public interface IValidadorOrigemMensagem
{
    string TipoOrigem { get; }

    /// <returns>O motivo para não enviar, ou <c>null</c> se a mensagem continua valendo.</returns>
    Task<string?> MotivoParaNaoEnviarAsync(Mensagem mensagem, CancellationToken ct = default);
}

/// <summary>
/// O lado de trabalho do motor único (BL-M09-01): manda uma mensagem de WhatsApp da fila, no escopo da escolinha dela.
/// Cada passo relê o estado (regra de ouro 4):
/// <list type="number">
/// <item>agendada para depois → reagenda;</item>
/// <item>a origem deixou de valer (compromisso que já passou, convite cancelado) → suprimida, com o motivo;</item>
/// <item>canal desligado ou número fora do teste (fora de produção) → retida ou suprimida;</item>
/// <item>janela de silêncio, exceto urgente → agendada para o fim da janela;</item>
/// <item>crédito: debita um, na mesma gravação da reserva de envio; sem saldo, a essencial fica retida e as demais são suprimidas (spec §8.2);</item>
/// <item>envio: sucesso grava o id da Meta e o índice global; falha retentável volta à fila; falha definitiva estorna o crédito.</item>
/// </list>
/// </summary>
public class ServicoEnvioMensagens(
    IMensageriaRepositorio repositorio,
    IConfiguracaoRepositorio configuracao,
    IParametros parametros,
    IGatewayWhatsApp gateway,
    IAgendadorMensageria agendador,
    IEnumerable<IValidadorOrigemMensagem> validadores,
    OpcoesWhatsApp opcoes,
    IAmbienteExecucao ambiente,
    IRelogioBrasil relogio,
    ILogger<ServicoEnvioMensagens> logger)
{
    /// <summary>Tentativas com falha retentável antes de desistir (a mensagem falha e o crédito volta).</summary>
    public const int MaxTentativas = 8;

    public static readonly TimeSpan DuracaoReserva = TimeSpan.FromMinutes(2);

    public const string MotivoSemCredito = "Sem créditos de mensagem: sai quando a escolinha tiver saldo.";
    public const string MotivoSemCreditoSuprimida = "Sem créditos de mensagem (não é essencial: não fica na fila).";
    public const string MotivoCanalDesligado = "WhatsApp desligado nesta instalação.";

    public async Task<SituacaoMensagem?> EnviarAsync(Guid mensagemId, CancellationToken ct = default)
    {
        var mensagem = await repositorio.ObterMensagemAsync(mensagemId, ct);
        if (mensagem is null) return null;
        if (mensagem.Canal != CanalMensagem.WhatsApp || mensagem.Situacao != SituacaoMensagem.Pendente) return mensagem.Situacao;

        var agora = relogio.Agora();
        if (mensagem.ReservadaPorOutro(agora)) return mensagem.Situacao;

        if (mensagem.AgendadaPara is { } quando && quando > agora)
        {
            agendador.Enviar(mensagem.Escolinha_ID, mensagem.Mensagem_ID, quando);
            return mensagem.Situacao;
        }

        if (await MotivoParaNaoEnviarAsync(mensagem, agora, ct) is { } motivo)
        {
            await EncerrarSemEnviarAsync(mensagem, motivo, reter: false, agora, ct);
            return mensagem.Situacao;
        }

        if (!opcoes.Habilitado)
        {
            await EncerrarSemEnviarAsync(mensagem, MotivoCanalDesligado, reter: mensagem.Essencial, agora, ct);
            return mensagem.Situacao;
        }

        if (opcoes.Api && !ambiente.EhProducao && !NumeroDeTeste(mensagem.Telefone))
        {
            await EncerrarSemEnviarAsync(mensagem,
                "Fora de produção, só os números de teste recebem WhatsApp (WhatsApp:NumerosTeste).", reter: false, agora, ct);
            return mensagem.Situacao;
        }

        var template = await repositorio.TemplatePorNomeAsync(mensagem.Template, ct);
        if (opcoes.Api && template?.AprovadoParaEnvio(mensagem.Template) != true)
        {
            await EncerrarSemEnviarAsync(mensagem, $"O template {mensagem.Template} não está aprovado na Meta.", reter: false, agora, ct);
            return mensagem.Situacao;
        }

        if (!mensagem.UrgenteEm(agora)
            && PoliticaEnvio.FimDaJanelaDeSilencio((await parametros.ObterAsync(ct)).Mensagem.JanelaSilencio, agora) is { } fim)
        {
            mensagem.Agendar(fim, $"Janela de silêncio: sai às {fim:HH:mm} de {fim:dd/MM}.", agora);
            await repositorio.SalvarAsync(ct);
            agendador.Enviar(mensagem.Escolinha_ID, mensagem.Mensagem_ID, fim);
            return mensagem.Situacao;
        }

        // Reserva de envio e crédito na mesma gravação: dois trabalhos não passam juntos (versões de linha da mensagem e da conta).
        if (!mensagem.CreditoReservado)
        {
            var conta = await configuracao.ObterContaCreditosAsync(ct);
            var movimento = conta?.Lancar(TipoMovimentoCredito.Consumo, -1,
                $"WhatsApp: {CatalogoGatilhos.Rotulo(mensagem.Gatilho)} para {mensagem.DestinatarioNome}", agora, null, "Motor de mensagens");
            if (movimento is null)
            {
                if (mensagem.Essencial) mensagem.Reter(MotivoSemCredito, agora);
                else mensagem.Suprimir(MotivoSemCreditoSuprimida, agora);
                await repositorio.SalvarAsync(ct);
                return mensagem.Situacao;
            }
            configuracao.AdicionarMovimentoCredito(movimento);
            mensagem.ReservarCredito();
        }
        mensagem.ReservarEnvio(agora.Add(DuracaoReserva));
        await repositorio.SalvarAsync(ct);

        try
        {
            var envio = await gateway.EnviarTemplateAsync(Pedido(mensagem, template), ct);
            var enviadaEm = relogio.Agora();
            mensagem.MarcarEnviada(envio.IdExterno, enviadaEm);
            repositorio.AdicionarRastreio(new RastreioWhatsApp
            {
                RastreioWhatsApp_ID = Guid.NewGuid(),
                IdExterno = envio.IdExterno,
                ChaveTelefone = Telefone.ChaveWhatsApp(mensagem.Telefone),
                Escolinha_ID = mensagem.Escolinha_ID,
                Mensagem_ID = mensagem.Mensagem_ID,
                EnviadoEm = enviadaEm
            });
            if (mensagem.Gatilho == GatilhoMensagem.ConviteAcesso) MascararLinkDoConvite(mensagem);
            await repositorio.SalvarAsync(ct);
            return mensagem.Situacao;
        }
        catch (FalhaIntegracaoException ex) when (ex.Retentavel && mensagem.Tentativas + 1 < MaxTentativas)
        {
            mensagem.RegistrarTentativa(ex.Message, relogio.Agora());
            await repositorio.SalvarAsync(ct);
            throw;
        }
        catch (FalhaIntegracaoException ex)
        {
            logger.LogWarning("Mensagem {Mensagem} não enviada: {Erro}", mensagem.Mensagem_ID, ex.Message);
            mensagem.Falhar(ex.Message, relogio.Agora());
            await EstornarAsync(mensagem, relogio.Agora(), ct);
            await repositorio.SalvarAsync(ct);
            return mensagem.Situacao;
        }
    }

    /// <summary>A mensagem que fala de um compromisso que já passou perdeu o sentido; senão, pergunta à origem.</summary>
    private async Task<string?> MotivoParaNaoEnviarAsync(Mensagem mensagem, DateTime agora, CancellationToken ct)
    {
        if (mensagem.Referencia is { } referencia && referencia < agora)
            return "O compromisso já passou: o aviso perdeu o sentido.";
        var validador = validadores.FirstOrDefault(v => v.TipoOrigem == mensagem.TipoOrigem);
        return validador is null ? null : await validador.MotivoParaNaoEnviarAsync(mensagem, ct);
    }

    private async Task EncerrarSemEnviarAsync(Mensagem mensagem, string motivo, bool reter, DateTime agora, CancellationToken ct)
    {
        if (reter) mensagem.Reter(motivo, agora);
        else
        {
            mensagem.Suprimir(motivo, agora);
            await EstornarAsync(mensagem, agora, ct);
        }
        await repositorio.SalvarAsync(ct);
    }

    /// <summary>Crédito reservado de mensagem que não saiu volta para a conta (lançamento de estorno no razão).</summary>
    private async Task EstornarAsync(Mensagem mensagem, DateTime agora, CancellationToken ct)
    {
        if (!mensagem.SoltarCredito()) return;
        var conta = await configuracao.ObterContaCreditosAsync(ct);
        if (conta?.Lancar(TipoMovimentoCredito.Estorno, 1, $"Estorno: {CatalogoGatilhos.Rotulo(mensagem.Gatilho)} para {mensagem.DestinatarioNome} não saiu",
                agora, null, "Motor de mensagens") is { } estorno)
            configuracao.AdicionarMovimentoCredito(estorno);
    }

    private bool NumeroDeTeste(string? telefone)
    {
        var chave = Telefone.ChaveWhatsApp(telefone);
        return opcoes.NumerosTeste.Any(n => Telefone.ChaveWhatsApp(n) == chave);
    }

    /// <summary>Os botões vêm do template gravado; sem ele (antes da carga do catálogo), do texto padrão.</summary>
    private static PedidoEnvioWhatsApp Pedido(Mensagem mensagem, TemplateMensagem? template)
    {
        var parametros = JsonSerializer.Deserialize<List<string>>(mensagem.Variaveis) ?? [];
        var botoes = template?.ListaBotoes
                     ?? (TemplatesPadrao.Todos.FirstOrDefault(p => p.NomeNaMeta == mensagem.Template) is { } padrao
                         ? TemplatesPadrao.Criar(padrao).ListaBotoes
                         : []);
        return new PedidoEnvioWhatsApp(mensagem.Mensagem_ID, mensagem.Escolinha_ID, Telefone.ParaWhatsApp(mensagem.Telefone!),
            mensagem.Template, template?.Idioma ?? "pt_BR", parametros, botoes, mensagem.Conteudo);
    }

    /// <summary>O link do convite tem o segredo: depois do envio, o conteúdo guardado fica com o link mascarado.</summary>
    private void MascararLinkDoConvite(Mensagem mensagem)
    {
        var prefixo = opcoes.LinkConvite(string.Empty);
        var inicio = mensagem.Conteudo.IndexOf(prefixo, StringComparison.Ordinal);
        if (inicio < 0) return;
        var fim = inicio + prefixo.Length;
        while (fim < mensagem.Conteudo.Length && (char.IsLetterOrDigit(mensagem.Conteudo[fim]) || mensagem.Conteudo[fim] is '-' or '_')) fim++;
        mensagem.MascararTrecho(mensagem.Conteudo[inicio..fim], prefixo + "••••••");
    }
}
