using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Configuracao;

namespace MinhaEscolinha.Application.Configuracao;

/// <summary>Plano, saldo e histórico de créditos de mensagem (BL-M02-11). O consumo por mensagem entra na S2.</summary>
public class ServicoCreditos(
    IConfiguracaoRepositorio repositorio,
    IParametros parametros,
    IContextoEscolinha contexto,
    IRelogioBrasil relogio,
    IUsuarioAtual usuarioAtual,
    RegistroDeAlteracoes registro)
{
    public async Task<CreditosResposta?> ObterAsync(int pagina, CancellationToken ct = default)
    {
        var conta = await repositorio.ObterContaCreditosAsync(ct);
        if (conta is null) return null;
        var valores = await parametros.ObterAsync(ct);
        var movimentos = await repositorio.ListarMovimentosCreditoAsync(Pagina<MovimentoCredito>.Normalizar(pagina), 20, ct);
        return new CreditosResposta(conta.Plano, conta.Pacote, conta.Saldo, valores.Mensagem.AvisoSaldoCreditosAbaixoDe, movimentos);
    }

    /// <summary>Concessão, recarga ou ajuste lançado pela equipe da plataforma (roda no escopo da escolinha).</summary>
    internal async Task<ResultadoOperacao> LancarDaPlataformaAsync(ConcederCreditosRequisicao r, CancellationToken ct)
    {
        if (!contexto.AdministradorPlataforma) return ResultadoOperacao.Erro("Só a equipe da plataforma concede créditos.");

        var conta = await repositorio.ObterContaCreditosAsync(ct);
        if (conta is null)
        {
            conta = new ContaCreditos();
            repositorio.AdicionarContaCreditos(conta);
        }

        if (!string.IsNullOrWhiteSpace(r.Plano)) AlterarPlano(conta, r.Plano.Trim(), r.Pacote);

        var tipo = r.Quantidade > 0 ? TipoMovimentoCredito.Concessao : TipoMovimentoCredito.Ajuste;
        var movimento = conta.Lancar(tipo, r.Quantidade, r.Motivo, relogio.Agora(), usuarioAtual.Id, usuarioAtual.Nome ?? "Plataforma");
        if (movimento is null)
            return ResultadoOperacao.Erro($"O saldo não pode ficar negativo (saldo atual: {conta.Saldo}).",
                nameof(ConcederCreditosRequisicao.Quantidade));
        repositorio.AdicionarMovimentoCredito(movimento);
        await repositorio.SalvarAsync(ct);

        return ResultadoOperacao.Ok($"{(r.Quantidade > 0 ? "Concedidos" : "Retirados")} {Math.Abs(r.Quantidade)} crédito(s). Saldo: {conta.Saldo}.");
    }

    private void AlterarPlano(ContaCreditos conta, string plano, string? pacote)
    {
        var novoPacote = string.IsNullOrWhiteSpace(pacote) ? null : pacote.Trim();
        registro.SeMudou(nameof(ContaCreditos), conta.ContaCreditos_ID == Guid.Empty ? null : conta.ContaCreditos_ID,
            "Plano", $"{conta.Plano} {conta.Pacote}".Trim(), $"{plano} {novoPacote}".Trim());
        conta.Plano = plano;
        conta.Pacote = novoPacote;
    }
}
