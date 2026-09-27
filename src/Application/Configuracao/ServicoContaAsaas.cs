using FluentValidation;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Configuracao;

namespace MinhaEscolinha.Application.Configuracao;

/// <summary>
/// Conta de recebimento Asaas da escolinha (BL-M02-10). Sem ela, nenhuma cobrança é emitida (a emissão entra na S4).
/// Vincular ou trocar pede a senha de quem faz, grava a chave cifrada e deixa o rastro na auditoria (sem a chave).
/// Fora de Production só se aceita conta Sandbox: um teste local nunca cobra de verdade.
/// </summary>
public class ServicoContaAsaas(
    IConfiguracaoRepositorio repositorio,
    IServicoAutenticacao autenticacao,
    IProtetorSegredos protetor,
    IAmbienteExecucao ambiente,
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
            ? new ContaAsaasResposta(false, null, null, null)
            : new ContaAsaasResposta(true, conta.Ambiente, conta.FinalChave, conta.VinculadaEm);
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

        var depois = $"{ContaAsaasEscolinha.RotuloAmbiente(conta.Ambiente)} · final {conta.FinalChave}";
        registro.Registrar(nameof(ContaAsaasEscolinha), null, "Conta de recebimento", antes, depois,
            antes is null ? "Conta vinculada." : "Conta substituída (chave de API alterada).");
        await repositorio.SalvarAsync(ct);

        logger.LogInformation("Conta Asaas da escolinha {Escolinha} {Acao} por {Autor} ({Ambiente}, final {Final}).",
            conta.Escolinha_ID, antes is null ? "vinculada" : "substituída", usuarioAtual.Login, conta.Ambiente, conta.FinalChave);
        return ResultadoOperacao.Ok(antes is null ? "Conta de recebimento vinculada." : "Conta de recebimento substituída.");
    }
}
