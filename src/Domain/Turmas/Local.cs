using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Turmas;

/// <summary>
/// Quadra ou local de treino e de jogo (spec §5): consumido pela turma (e pelas sessões geradas dela) e, no S5, pelo evento.
/// Os "horários disponíveis" do local ficam para o alerta de conflito de quadra (BL-M04-10, adiado no piloto);
/// a ocupação do local já aparece na agenda (BL-M04-08).
/// </summary>
public class Local : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoNome = 100;
    public const int TamanhoObservacao = 300;

    public Guid Local_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public string Nome { get; set; } = string.Empty;
    public Endereco Endereco { get; set; } = new();

    /// <summary>Quantas pessoas o local comporta (opcional, informativo).</summary>
    public int? Capacidade { get; set; }

    /// <summary>Como chegar, portão de entrada, estacionamento.</summary>
    public string? Observacao { get; set; }

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }
}
