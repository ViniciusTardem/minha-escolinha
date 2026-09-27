using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Configuracao;

public enum TipoMovimentoCredito
{
    /// <summary>Créditos concedidos pela equipe da plataforma (pacote do plano, recarga).</summary>
    Concessao = 10,

    /// <summary>Uma mensagem de WhatsApp enviada (fatia S2).</summary>
    Consumo = 20,

    /// <summary>Devolução de crédito de envio que falhou de forma definitiva (fatia S2).</summary>
    Estorno = 30,

    /// <summary>Correção manual da equipe da plataforma, com motivo.</summary>
    Ajuste = 40
}

/// <summary>
/// Plano, pacote e saldo de créditos de mensagem da escolinha (BL-M02-11, RF-MSG-004). O saldo é sempre a soma do
/// razão (<see cref="MovimentoCredito"/>) e nunca fica negativo. A linha tem versão: dois consumos simultâneos não
/// passam pelo mesmo saldo.
/// </summary>
public class ContaCreditos : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoPlano = 80;
    public const string PlanoPiloto = "Piloto";

    public Guid ContaCreditos_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public string Plano { get; set; } = PlanoPiloto;
    public string? Pacote { get; set; }
    public int Saldo { get; set; }

    /// <summary>Concorrência otimista (rowversion).</summary>
    public byte[] Versao { get; set; } = [];

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    /// <summary>
    /// Lança um movimento e devolve a linha do razão, ou <c>null</c> se o saldo ficaria negativo (o chamador transforma em erro).
    /// </summary>
    public MovimentoCredito? Lancar(TipoMovimentoCredito tipo, int quantidade, string motivo, DateTime agora,
        Guid? autorId, string autorNome)
    {
        if (quantidade == 0) return null;
        if (Saldo + quantidade < 0) return null;

        Saldo += quantidade;
        return new MovimentoCredito
        {
            Escolinha_ID = Escolinha_ID,
            Tipo = tipo,
            Quantidade = quantidade,
            SaldoApos = Saldo,
            Motivo = TextoHelper.Truncar(motivo, MovimentoCredito.TamanhoMotivo) ?? string.Empty,
            Em = agora,
            Autor_ID = autorId,
            AutorNome = TextoHelper.Truncar(autorNome, MovimentoCredito.TamanhoAutor) ?? string.Empty
        };
    }
}

/// <summary>Linha do razão de créditos: quem, quando, quanto e o saldo depois.</summary>
public class MovimentoCredito : IEntidadeDaEscolinha
{
    public const int TamanhoMotivo = 300;
    public const int TamanhoAutor = 150;

    public Guid MovimentoCredito_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public TipoMovimentoCredito Tipo { get; set; }

    /// <summary>Positivo na concessão e no estorno, negativo no consumo.</summary>
    public int Quantidade { get; set; }

    public int SaldoApos { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateTime Em { get; set; }
    public Guid? Autor_ID { get; set; }
    public string AutorNome { get; set; } = string.Empty;

    public static string RotuloTipo(TipoMovimentoCredito tipo) => tipo switch
    {
        TipoMovimentoCredito.Concessao => "Concessão",
        TipoMovimentoCredito.Consumo => "Consumo",
        TipoMovimentoCredito.Estorno => "Estorno",
        TipoMovimentoCredito.Ajuste => "Ajuste",
        _ => tipo.ToString()
    };
}
