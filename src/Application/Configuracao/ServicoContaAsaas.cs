using FluentValidation;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Financeiro;
using MinhaEscolinha.Application.Integracao.Asaas;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Financeiro;

namespace MinhaEscolinha.Application.Configuracao;

/// <summary>
/// Conta de recebimento Asaas da escolinha (BL-M02-10). Sem ela, nenhuma cobrança é emitida.
/// Vincular ou trocar pede a senha de quem faz, grava a chave cifrada e deixa o rastro na auditoria (sem a chave). Depois,
/// as mensalidades prontas vão para a Asaas num job, e as que a Asaas tinha recusado tentam de novo (D73).
/// Fora de Production só se aceita conta Sandbox: um teste local nunca cobra de verdade.
/// </summary>
public class ServicoContaAsaas(
    IConfiguracaoRepositorio repositorio,
    IFinanceiroRepositorio financeiro,
    IServicoAutenticacao autenticacao,
    IProtetorSegredos protetor,
    IAmbienteExecucao ambiente,
    IAgendadorFinanceiro agendador,
    OpcoesAsaas opcoes,
    IRelogioBrasil relogio,
    IUsuarioAtual usuarioAtual,
    RegistroDeAlteracoes registro,
    IValidator<VincularContaAsaasRequisicao> validador,
    ILogger<ServicoContaAsaas> logger)
{
    public async Task<ContaAsaasResposta> ObterAsync(CancellationToken ct = default)
    {
        var conta = await repositorio.ObterContaAsaasAsync(ct);
        return conta is null
            ? new ContaAsaasResposta(false, null, null, null, Integracao: opcoes.Descricao, Simulador: opcoes.Simulado)
            : new ContaAsaasResposta(true, conta.Ambiente, conta.FinalChave, conta.VinculadaEm, opcoes.UrlWebhook(conta.Escolinha_ID),
                conta.WebhookRegistradoEm, conta.WebhookErro, opcoes.Descricao, opcoes.Simulado);
    }

    /// <summary>Condição de emissão de qualquer cobrança (a S4 consulta aqui antes de emitir).</summary>
    public async Task<bool> PodeEmitirCobrancaAsync(CancellationToken ct = default) =>
        await repositorio.ObterContaAsaasAsync(ct) is not null;

    public async Task<ResultadoOperacao> VincularAsync(VincularContaAsaasRequisicao requisicao, CancellationToken ct = default)
    {
        var validacao = await validador.ValidateAsync(requisicao, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);

        if (requisicao.Ambiente == AmbienteAsaas.Producao && !ambiente.EhProducao)
            return ResultadoOperacao.Erro(
                $"Este ambiente ({ambiente.Nome}) só aceita conta Sandbox da Asaas. Conta de produção só no sistema em produção.",
                nameof(VincularContaAsaasRequisicao.Ambiente));

        if (!await autenticacao.ConferirSenhaAsync(requisicao.SenhaAtual, ct))
            return ResultadoOperacao.Erro("A senha não confere. Trocar a conta de recebimento pede a sua senha.",
                nameof(VincularContaAsaasRequisicao.SenhaAtual));

        var chave = requisicao.ChaveApi.Trim();
        var conta = await repositorio.ObterContaAsaasAsync(ct);
        var antes = conta is null ? null : $"{ContaAsaasEscolinha.RotuloAmbiente(conta.Ambiente)} · final {conta.FinalChave}";
        if (conta is null)
        {
            conta = new ContaAsaasEscolinha();
            repositorio.AdicionarContaAsaas(conta);
        }

        conta.Ambiente = requisicao.Ambiente;
        conta.ChaveApiProtegida = protetor.Proteger(chave);
        conta.FinalChave = ContaAsaasEscolinha.CalcularFinal(chave);
        conta.VinculadaEm = relogio.Agora();
        conta.VinculadaPor_ID = usuarioAtual.Id;
        conta.NovaVersao();
        // O token do webhook nasce uma vez e fica: os avisos da conta anterior continuam valendo para o que foi emitido nela.
        conta.TokenWebhookProtegido ??= protetor.Proteger(TokenWebhookAsaas.Gerar());

        // A chave nova não enxerga o que ficou na conta anterior: as cobranças em aberto de lá saem da régua (S4).
        var daAnterior = 0;
        if (antes is not null)
            foreach (var cobranca in await financeiro.EmAbertoAsync(ct))
            {
                if (cobranca.ContaAsaas == conta.Identificador) continue;
                cobranca.Sinalizar(SinalCobranca.ContaAnterior,
                    "Emitida na conta Asaas anterior à troca: confira na conta antiga se foi paga.", relogio.Agora());
                daAnterior++;
            }

        // A recusa pode ter sido da conta (chave inválida ou revogada): com a conta nova, as recusadas tentam de novo.
        var recusadas = await financeiro.RecusadasAsync(ct);
        foreach (var cobranca in recusadas) cobranca.LiberarNovaTentativa();

        var depois = $"{ContaAsaasEscolinha.RotuloAmbiente(conta.Ambiente)} · final {conta.FinalChave}";
        registro.Registrar(nameof(ContaAsaasEscolinha), null, "Conta de recebimento", antes, depois,
            antes is null ? "Conta vinculada." : "Conta substituída (chave de API alterada).");
        await repositorio.SalvarAsync(ct);
        if (opcoes.Api) agendador.RegistrarWebhook(conta.Escolinha_ID);
        // As previstas que esperavam a conta (e as liberadas) vão para a Asaas sem esperar a rotina da madrugada.
        agendador.Projetar(conta.Escolinha_ID);

        logger.LogInformation("Conta Asaas da escolinha {Escolinha} {Acao} por {Autor} ({Ambiente}, final {Final}).",
            conta.Escolinha_ID, antes is null ? "vinculada" : "substituída", usuarioAtual.Login, conta.Ambiente, conta.FinalChave);
        return ResultadoOperacao.Ok((antes is null ? "Conta de recebimento vinculada." : "Conta de recebimento substituída.")
                                    + (daAnterior > 0 ? $" {daAnterior} mensalidade(s) em aberto na conta anterior saíram da régua." : string.Empty)
                                    + (recusadas.Count > 0 ? $" {recusadas.Count} mensalidade(s) recusada(s) pela Asaas voltaram para a emissão." : string.Empty));
    }
}
