using FluentValidation;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Integracao.Stripe;
using MinhaEscolinha.Domain.Integracao;
using MinhaEscolinha.Domain.Plataforma;

namespace MinhaEscolinha.Application.Plataforma;

/// <summary>Catálogo de pacotes de créditos (equipe da plataforma) e a situação da integração com o Stripe.</summary>
public class ServicoPacotesCreditos(
    IPlataformaRepositorio repositorio,
    IContextoEscolinha contexto,
    IValidator<PacoteRequisicao> validador,
    OpcoesStripe opcoes)
{
    public async Task<IReadOnlyList<PacoteResposta>> ListarAsync(CancellationToken ct = default) =>
        (await repositorio.ListarPacotesAsync(somenteAtivos: false, ct)).Select(PacoteResposta.De).ToList();

    public async Task<PacoteRequisicao?> ObterParaEdicaoAsync(Guid id, CancellationToken ct = default) =>
        await repositorio.ObterPacoteAsync(id, ct) is { } p
            ? new PacoteRequisicao { Id = p.PacoteCreditos_ID, Nome = p.Nome, Creditos = p.Creditos, Preco = p.Preco, Ativo = p.Ativo, Ordem = p.Ordem }
            : null;

    /// <summary>Cria ou altera. O preço de uma compra já feita não muda: a compra guarda o dela.</summary>
    public async Task<ResultadoOperacao> SalvarAsync(PacoteRequisicao requisicao, CancellationToken ct = default)
    {
        if (!contexto.AdministradorPlataforma) return ResultadoOperacao.Erro("Só a equipe da plataforma mantém os pacotes de créditos.");

        var validacao = await validador.ValidateAsync(requisicao, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);

        PacoteCreditos pacote;
        if (requisicao.Id is { } id)
        {
            if (await repositorio.ObterPacoteAsync(id, ct) is not { } existente) return ResultadoOperacao.Erro("Pacote não encontrado.");
            pacote = existente;
        }
        else
        {
            pacote = new PacoteCreditos();
            repositorio.AdicionarPacote(pacote);
        }

        pacote.Nome = requisicao.Nome.Trim();
        pacote.Creditos = requisicao.Creditos;
        pacote.Preco = Math.Round(requisicao.Preco, 2);
        pacote.Ativo = requisicao.Ativo;
        pacote.Ordem = requisicao.Ordem;
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Pacote {pacote.Nome} salvo.", pacote.PacoteCreditos_ID);
    }

    public async Task<IntegracaoStripeResposta> IntegracaoAsync(CancellationToken ct = default)
    {
        var webhooks = await repositorio.ListarWebhooksAsync(WebhookRecebido.OrigemStripe, 10, ct);
        return new IntegracaoStripeResposta(opcoes.Habilitado, opcoes.Descricao, opcoes.UrlWebhook,
            opcoes.Simulado || !string.IsNullOrWhiteSpace(opcoes.SegredoWebhook), EventosStripe.Tratados,
            webhooks.Select(WebhookResumo.De).ToList());
    }
}
