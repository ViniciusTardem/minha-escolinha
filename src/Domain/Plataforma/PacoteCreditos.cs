using System.Globalization;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Plataforma;

/// <summary>
/// Pacote de créditos de mensagem que a LDNA vende às escolinhas, pago pelo Stripe da LDNA. Catálogo global: não pertence
/// a escolinha nenhuma. Preço e quantidade são decisão comercial da equipe da plataforma (DECISOES P26).
/// </summary>
public class PacoteCreditos : IEntidadeAuditavel
{
    public const int TamanhoNome = 80;
    public const int CreditosMaximo = 1_000_000;

    /// <summary>O Stripe não cobra menos de R$ 0,50; R$ 1,00 dá folga.</summary>
    public const decimal PrecoMinimo = 1m;

    public const decimal PrecoMaximo = 100_000m;

    private static readonly CultureInfo PtBr = new("pt-BR");

    public Guid PacoteCreditos_ID { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int Creditos { get; set; }
    public decimal Preco { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Posição na vitrine (menor primeiro).</summary>
    public int Ordem { get; set; }

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    /// <summary>A linha na página de pagamento do Stripe e no histórico: "Básico — 500 créditos de mensagem".</summary>
    public string Descricao() => $"{Nome} — {Creditos.ToString("N0", PtBr)} créditos de mensagem";
}
