using FluentValidation;
using MinhaEscolinha.Domain.Integracao;
using MinhaEscolinha.Domain.Plataforma;

namespace MinhaEscolinha.Application.Plataforma;

public class PacoteRequisicao
{
    public Guid? Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int Creditos { get; set; }
    public decimal Preco { get; set; }
    public bool Ativo { get; set; } = true;
    public int Ordem { get; set; }
}

public class PacoteRequisicaoValidador : AbstractValidator<PacoteRequisicao>
{
    public PacoteRequisicaoValidador()
    {
        RuleFor(p => p.Nome).NotEmpty().WithMessage("Informe o nome do pacote.")
            .MaximumLength(PacoteCreditos.TamanhoNome).WithMessage($"O nome vai até {PacoteCreditos.TamanhoNome} caracteres.");
        RuleFor(p => p.Creditos).InclusiveBetween(1, PacoteCreditos.CreditosMaximo)
            .WithMessage("Informe a quantidade de créditos (de 1 a 1.000.000).");
        RuleFor(p => p.Preco).InclusiveBetween(PacoteCreditos.PrecoMinimo, PacoteCreditos.PrecoMaximo)
            .WithMessage("O preço vai de R$ 1,00 a R$ 100.000,00.")
            .PrecisionScale(10, 2, ignoreTrailingZeros: true).WithMessage("O preço tem no máximo 2 casas decimais.");
        RuleFor(p => p.Ordem).InclusiveBetween(0, 999).WithMessage("A ordem vai de 0 a 999.");
    }
}

public record PacoteResposta(Guid Id, string Nome, int Creditos, decimal Preco, bool Ativo, int Ordem)
{
    public static PacoteResposta De(PacoteCreditos p) => new(p.PacoteCreditos_ID, p.Nome, p.Creditos, p.Preco, p.Ativo, p.Ordem);
}

public record CompraResposta(
    Guid Id,
    string NomePacote,
    int Creditos,
    decimal Valor,
    SituacaoCompraCreditos Situacao,
    string? MeioPagamento,
    DateTime IniciadaEm,
    DateTime? ConcluidaEm,
    string AutorNome,
    string? Erro,
    bool Final)
{
    public static CompraResposta De(CompraCreditos c) => new(c.CompraCreditos_ID, c.NomePacote, c.Creditos, c.Valor, c.Situacao,
        c.MeioPagamento, c.IniciadaEm, c.ConcluidaEm, c.AutorNome, c.Erro, c.Final);

    /// <summary>"cartão", "boleto" ou "Pix" (como o Stripe informa: card, boleto, pix).</summary>
    public string? RotuloMeio => MeioPagamento switch
    {
        null => null,
        "card" => "cartão",
        "boleto" => "boleto",
        "pix" => "Pix",
        var outro => outro
    };
}

/// <summary>A vitrine de pacotes e as compras da escolinha, na tela de créditos.</summary>
public record TelaCompraCreditos(bool Habilitado, string DescricaoIntegracao, IReadOnlyList<PacoteResposta> Pacotes, IReadOnlyList<CompraResposta> Compras);

/// <summary>Como a integração está, para a equipe da plataforma configurar o endpoint no painel do Stripe.</summary>
public record IntegracaoStripeResposta(
    bool Habilitado,
    string Descricao,
    string UrlWebhook,
    bool SegredoWebhookConfigurado,
    IReadOnlyList<string> EventosAssinar,
    IReadOnlyList<WebhookResumo> UltimosWebhooks);

public record WebhookResumo(DateTime RecebidoEm, string? Tipo, SituacaoWebhook Situacao, string? Observacao)
{
    public static WebhookResumo De(WebhookRecebido w) => new(w.RecebidoEm, w.Tipo, w.Situacao, w.Observacao);
}
