using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Plataforma;

public enum SituacaoCompraCreditos
{
    /// <summary>Sessão de pagamento aberta no Stripe; a pessoa ainda não pagou.</summary>
    AguardandoPagamento = 10,

    /// <summary>Boleto ou Pix emitido: o Stripe avisa quando o banco confirmar.</summary>
    AguardandoConfirmacao = 20,

    Paga = 30,

    /// <summary>A sessão de pagamento venceu sem pagamento.</summary>
    Expirada = 40,

    /// <summary>Não deu para abrir o pagamento, ou o boleto/Pix não foi pago.</summary>
    Falhou = 50
}

/// <summary>
/// Compra de um pacote de créditos por uma escolinha, paga pelo Stripe da LDNA. Nome, créditos e valor ficam congelados
/// na compra: mudar o pacote depois não muda o que foi comprado. As transições são explícitas; os créditos só entram
/// quando o Stripe confirma o pagamento, e uma vez só (<see cref="ConfirmarPagamento"/> devolve false na repetição).
/// </summary>
public class CompraCreditos : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoReferenciaExterna = 255;
    public const int TamanhoMeio = 30;
    public const int TamanhoAutor = 150;
    public const int TamanhoErro = 500;

    public Guid CompraCreditos_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid PacoteCreditos_ID { get; set; }

    public string NomePacote { get; set; } = string.Empty;
    public int Creditos { get; set; }
    public decimal Valor { get; set; }

    public SituacaoCompraCreditos Situacao { get; private set; } = SituacaoCompraCreditos.AguardandoPagamento;

    /// <summary>Id da sessão de Checkout no Stripe (<c>cs_...</c>). A URL da sessão expira e não é gravada (regra de ouro 3).</summary>
    public string? SessaoCheckoutId { get; set; }

    /// <summary>Id do pagamento no Stripe (<c>pi_...</c>), para conferir no painel do Stripe.</summary>
    public string? PagamentoId { get; private set; }

    /// <summary>card · boleto · pix (como o Stripe informa).</summary>
    public string? MeioPagamento { get; private set; }

    public DateTime IniciadaEm { get; set; }
    public DateTime? ExpiraEm { get; set; }
    public DateTime? ConcluidaEm { get; private set; }
    public string? Erro { get; private set; }
    public Guid? Autor_ID { get; set; }
    public string AutorNome { get; set; } = string.Empty;

    /// <summary>Versão de linha: duas confirmações simultâneas não creditam duas vezes (a segunda falha e é refeita).</summary>
    public byte[] Versao { get; set; } = [];

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    public bool Final => Situacao is SituacaoCompraCreditos.Paga or SituacaoCompraCreditos.Expirada or SituacaoCompraCreditos.Falhou;

    /// <summary>O id nasce aqui: ele vai para o Stripe (referência e metadados) antes de a compra ser gravada de novo.</summary>
    public static CompraCreditos Nova(PacoteCreditos pacote, DateTime agora, Guid? autorId, string autorNome) => new()
    {
        CompraCreditos_ID = Guid.NewGuid(),
        PacoteCreditos_ID = pacote.PacoteCreditos_ID,
        NomePacote = pacote.Nome,
        Creditos = pacote.Creditos,
        Valor = pacote.Preco,
        IniciadaEm = agora,
        Autor_ID = autorId,
        AutorNome = TextoHelper.Truncar(autorNome, TamanhoAutor) ?? string.Empty
    };

    /// <summary>
    /// O Stripe confirmou o pagamento. <c>false</c> = já estava paga (evento repetido ou fora de ordem): nada muda e
    /// nenhum crédito é lançado de novo. Vale também depois de expirada ou falha: o Stripe só confirma o que foi pago.
    /// </summary>
    public bool ConfirmarPagamento(DateTime agora, string? pagamentoId, string? meio)
    {
        if (Situacao == SituacaoCompraCreditos.Paga) return false;
        Situacao = SituacaoCompraCreditos.Paga;
        ConcluidaEm = agora;
        PagamentoId = TextoHelper.Truncar(pagamentoId, TamanhoReferenciaExterna) ?? PagamentoId;
        MeioPagamento = TextoHelper.Truncar(meio, TamanhoMeio) ?? MeioPagamento;
        Erro = null;
        return true;
    }

    /// <summary>Boleto ou Pix emitido na página do Stripe; o pagamento chega depois.</summary>
    public bool AguardarConfirmacao(string? meio)
    {
        if (Situacao != SituacaoCompraCreditos.AguardandoPagamento) return false;
        Situacao = SituacaoCompraCreditos.AguardandoConfirmacao;
        MeioPagamento = TextoHelper.Truncar(meio, TamanhoMeio) ?? MeioPagamento;
        return true;
    }

    public bool Expirar(DateTime agora)
    {
        if (Situacao is not (SituacaoCompraCreditos.AguardandoPagamento or SituacaoCompraCreditos.AguardandoConfirmacao)) return false;
        Situacao = SituacaoCompraCreditos.Expirada;
        ConcluidaEm = agora;
        return true;
    }

    public bool Falhar(DateTime agora, string motivo)
    {
        if (Final) return false;
        Situacao = SituacaoCompraCreditos.Falhou;
        ConcluidaEm = agora;
        Erro = TextoHelper.Truncar(motivo, TamanhoErro);
        return true;
    }
}
