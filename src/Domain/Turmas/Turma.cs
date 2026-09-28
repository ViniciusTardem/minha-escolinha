using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Turmas;

public enum SituacaoTurma
{
    Ativa = 10,

    /// <summary>Não recebe matrícula nem gera sessão; continua no histórico com as sessões e matrículas que teve.</summary>
    Encerrada = 20
}

/// <summary>
/// Turma (BL-M04-01): modalidade, categoria, horários, local, capacidade, professor responsável, mensalidade,
/// meta de frequência e mínimo de confirmados por jogo. As sessões de treino são geradas dos horários (BL-M04-04).
/// <para>
/// Capacidade, mensalidade e mínimo de confirmados são os parâmetros de nível de turma (BL-M02-12): a turma nova
/// nasce com o valor da escolinha e passa a ter o seu. Mudar a mensalidade é ação sensível (fica no registro de alterações).
/// </para>
/// </summary>
public class Turma : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoNome = 100;
    public const int TamanhoCategoria = 60;
    public const int TamanhoMotivo = 300;
    public const int CapacidadeMaxima = 200;
    public const decimal MensalidadeMaxima = 100000m;
    public const int MinimoConfirmadosMaximo = 30;

    public Guid Turma_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public string Nome { get; set; } = string.Empty;
    public Guid Modalidade_ID { get; set; }

    /// <summary>Categoria ou faixa etária como a escolinha chama ("Sub-9", "2015 e 2016").</summary>
    public string? Categoria { get; set; }

    public Guid Local_ID { get; set; }
    public Local? Local { get; set; }

    /// <summary>Professor responsável: escopo do professor (BL-M08-05) e professor das sessões geradas.</summary>
    public Guid Professor_ID { get; set; }
    public Equipe.Professor? Professor { get; set; }

    public int Capacidade { get; set; }

    /// <summary>
    /// O valor de cada cobrança da turma — o valor inicial de cada matrícula nela (a matrícula pode ter o seu). Na turma
    /// mensal é a mensalidade; na trimestral, semestral ou anual, o valor do período (P44).
    /// </summary>
    public decimal Mensalidade { get; set; }

    /// <summary>De quantos em quantos meses a turma é cobrada: 1 (mensal), 3, 6 ou 12 (P44).</summary>
    public int PeriodicidadeMeses { get; set; } = 1;

    public static readonly IReadOnlyList<int> Periodicidades = [1, 3, 6, 12];

    public static string RotuloPeriodicidade(int meses) => meses switch
    {
        3 => "Trimestral",
        6 => "Semestral",
        12 => "Anual",
        _ => "Mensal"
    };

    public int MinimoConfirmados { get; set; }

    /// <summary>
    /// Meta de frequência da turma (spec §5), em %. Vazia = vale a frequência mínima da escolinha
    /// (parâmetro <c>presenca.risco.frequencia_minima</c>), usada no risco de evasão (S3).
    /// </summary>
    public int? MetaFrequenciaPercentual { get; set; }

    /// <summary>A partir de quando a turma treina (as sessões começam aqui ou hoje, o que vier depois).</summary>
    public DateOnly DataInicio { get; set; }

    public SituacaoTurma Situacao { get; set; } = SituacaoTurma.Ativa;
    public DateTime? EncerradaEm { get; set; }
    public string? MotivoEncerramento { get; set; }

    public List<HorarioTurma> Horarios { get; set; } = [];

    public byte[] Versao { get; set; } = [];

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    public bool Ativa => Situacao == SituacaoTurma.Ativa;

    public void Encerrar(DateTime agora, string motivo)
    {
        if (!Ativa) throw new InvalidOperationException("A turma já está encerrada.");
        Situacao = SituacaoTurma.Encerrada;
        EncerradaEm = agora;
        MotivoEncerramento = TextoHelper.Truncar(motivo, TamanhoMotivo);
    }

    /// <summary>"Terça e quinta, 18:00–19:00" ou os horários um a um quando diferem.</summary>
    public string ResumoHorarios() => ResumirHorarios(Horarios.Select(h => h.Semanal));

    public static string ResumirHorarios(IEnumerable<HorarioSemanal> horarios)
    {
        var ordenados = horarios.OrderBy(h => DiasSemana.Posicao(h.Dia)).ThenBy(h => h.Inicio).ToList();
        if (ordenados.Count == 0) return "Sem horário";
        var grupos = ordenados.GroupBy(h => (h.Inicio, h.Fim)).ToList();
        if (grupos.Count == 1)
        {
            var dias = ordenados.Select(h => DiasSemana.Nome(h.Dia)).ToList();
            var lista = dias.Count == 1 ? dias[0] : string.Join(", ", dias.Take(dias.Count - 1)) + " e " + dias[^1];
            return $"{lista}, {ordenados[0].Inicio:HH\\:mm}–{ordenados[0].Fim:HH\\:mm}";
        }
        return string.Join(" · ", ordenados.Select(h => h.Descricao()));
    }
}

/// <summary>Um horário semanal da turma. Cada sessão gerada guarda de qual horário veio (para acompanhar a mudança dele).</summary>
public class HorarioTurma : IEntidadeDaEscolinha
{
    public Guid HorarioTurma_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Turma_ID { get; set; }
    public DayOfWeek DiaSemana { get; set; }
    public TimeOnly Inicio { get; set; }
    public TimeOnly Fim { get; set; }

    public HorarioSemanal Semanal => new(DiaSemana, Inicio, Fim);
}
