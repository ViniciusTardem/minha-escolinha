using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Alunos;

/// <summary>Máquina do aluno (spec §6). Valores espaçados de 10 (gravados no banco).</summary>
public enum SituacaoAluno
{
    /// <summary>Aula experimental até <see cref="Aluno.TrialAte"/>. Vencido o prazo, continua em trial, sinalizado.</summary>
    Trial = 10,

    Ativo = 20,

    /// <summary>Pausa temporária: sem cobrança e sem chamada.</summary>
    Inativo = 30,

    /// <summary>Desligado com data e motivo: encerra as matrículas e libera a vaga, preservando o histórico.</summary>
    Desistente = 40
}

/// <summary>
/// As transições da spec §6, explícitas — e nenhuma automática (RF-ALU-006): o sistema nunca desliga nem converte ninguém sozinho.
/// <c>trial → ativo | desistente</c> · <c>ativo ↔ inativo</c> · <c>ativo → desistente</c> · <c>desistente → ativo</c> (rematrícula),
/// mais o atalho <c>inativo → desistente</c>, fora da §6 e aprovado pelo responsável em 27/09/2026 (P28, D43): quem está em pausa
/// é desligado direto, sem reativar antes.
/// </summary>
public static class MaquinaSituacaoAluno
{
    private static readonly HashSet<(SituacaoAluno De, SituacaoAluno Para)> Transicoes =
    [
        (SituacaoAluno.Trial, SituacaoAluno.Ativo),
        (SituacaoAluno.Trial, SituacaoAluno.Desistente),
        (SituacaoAluno.Ativo, SituacaoAluno.Inativo),
        (SituacaoAluno.Inativo, SituacaoAluno.Ativo),
        (SituacaoAluno.Ativo, SituacaoAluno.Desistente),
        (SituacaoAluno.Inativo, SituacaoAluno.Desistente),
        (SituacaoAluno.Desistente, SituacaoAluno.Ativo)
    ];

    public static bool Permite(SituacaoAluno de, SituacaoAluno para) => Transicoes.Contains((de, para));

    public static IEnumerable<SituacaoAluno> Destinos(SituacaoAluno de) =>
        Transicoes.Where(t => t.De == de).Select(t => t.Para).OrderBy(p => p);

    public static IEnumerable<(SituacaoAluno De, SituacaoAluno Para)> Todas => Transicoes;

    public static string Rotulo(SituacaoAluno situacao) => situacao switch
    {
        SituacaoAluno.Trial => "Trial",
        SituacaoAluno.Ativo => "Ativo",
        SituacaoAluno.Inativo => "Inativo",
        SituacaoAluno.Desistente => "Desistente",
        _ => situacao.ToString()
    };

    /// <summary>O nome da ação que leva de uma situação à outra, como aparece no botão.</summary>
    public static string Acao(SituacaoAluno de, SituacaoAluno para) => (de, para) switch
    {
        (SituacaoAluno.Trial, SituacaoAluno.Ativo) => "Confirmar matrícula",
        (SituacaoAluno.Ativo, SituacaoAluno.Inativo) => "Pausar",
        (SituacaoAluno.Inativo, SituacaoAluno.Ativo) => "Reativar",
        (SituacaoAluno.Desistente, SituacaoAluno.Ativo) => "Rematricular",
        (_, SituacaoAluno.Desistente) => "Desligar",
        _ => Rotulo(para)
    };
}

/// <summary>
/// Aluno (BL-M03-02): identificação, endereço, categoria, situação, datas de matrícula e de desligamento com motivo,
/// observações médicas e contato de emergência (BL-M03-11). Não tem telefone próprio: toda comunicação vai para os
/// responsáveis (R4), por construção.
/// <para>
/// Alergias, restrições, observações médicas e contato de emergência são dado sensível (R5): só saem do banco no
/// modelo da ficha, filtrados pela política de saúde — nunca numa lista.
/// </para>
/// </summary>
public class Aluno : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoNome = 150;
    public const int TamanhoCategoria = 60;
    public const int TamanhoMotivo = 300;
    public const int TamanhoParentesco = 60;
    public const int TamanhoTelefone = 20;
    public const int TamanhoSaude = 500;
    public const int TamanhoObservacoesMedicas = 1000;

    public Guid Aluno_ID { get; set; }
    public Guid Escolinha_ID { get; set; }

    public string Nome { get; private set; } = string.Empty;
    public string NomeBusca { get; private set; } = string.Empty;
    public DateOnly DataNascimento { get; set; }

    /// <summary>Só os 11 dígitos, se o aluno tiver; único na escolinha quando informado.</summary>
    public string? Cpf { get; set; }

    public string? Categoria { get; set; }
    public Endereco Endereco { get; set; } = new();

    public SituacaoAluno Situacao { get; private set; } = SituacaoAluno.Ativo;

    /// <summary>Desde quando está na situação atual.</summary>
    public DateOnly SituacaoDesde { get; private set; }

    /// <summary>Data limite do trial (só faz sentido em <see cref="SituacaoAluno.Trial"/>; fica no registro depois).</summary>
    public DateOnly? TrialAte { get; private set; }

    /// <summary>Data da matrícula na escolinha; a rematrícula grava a nova.</summary>
    public DateOnly DataMatricula { get; private set; }

    public DateOnly? DataDesligamento { get; private set; }
    public string? MotivoDesligamento { get; private set; }

    public string? ContatoEmergenciaNome { get; set; }
    public string? ContatoEmergenciaTelefone { get; set; }
    public string? ContatoEmergenciaParentesco { get; set; }
    public string? Alergias { get; set; }
    public string? Restricoes { get; set; }
    public string? ObservacoesMedicas { get; set; }

    public byte[] Versao { get; set; } = [];

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    /// <summary>Aluno novo: ativo, ou em trial até a data informada.</summary>
    public static Aluno Novo(string nome, DateOnly dataNascimento, DateOnly dataMatricula, DateOnly? trialAte)
    {
        var aluno = new Aluno { DataNascimento = dataNascimento };
        aluno.DefinirNome(nome);
        aluno.DataMatricula = dataMatricula;
        aluno.SituacaoDesde = dataMatricula;
        aluno.Situacao = trialAte is null ? SituacaoAluno.Ativo : SituacaoAluno.Trial;
        aluno.TrialAte = trialAte;
        return aluno;
    }

    public void DefinirNome(string nome)
    {
        Nome = nome.Trim();
        NomeBusca = TextoHelper.NormalizarParaBusca(Nome);
    }

    public void AlterarPrazoTrial(DateOnly ate)
    {
        if (Situacao != SituacaoAluno.Trial) throw new InvalidOperationException("Só o aluno em trial tem prazo de trial.");
        TrialAte = ate;
    }

    /// <summary>Todas as transições passam por aqui (validadas pela <see cref="MaquinaSituacaoAluno"/>).</summary>
    /// <param name="motivo">Obrigatório no desligamento; opcional nas demais (vai para o registro de alterações).</param>
    public void MudarSituacao(SituacaoAluno para, DateOnly data, string? motivo = null)
    {
        if (!MaquinaSituacaoAluno.Permite(Situacao, para))
            throw new InvalidOperationException($"Transição inválida: {Situacao} → {para}.");

        if (para == SituacaoAluno.Desistente)
        {
            if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("O desligamento exige motivo.", nameof(motivo));
            if (data < DataMatricula) throw new ArgumentException("O desligamento não pode ser antes da matrícula.", nameof(data));
            if (data < SituacaoDesde) throw new ArgumentException("O desligamento não pode ser antes do início da situação atual.", nameof(data));
            DataDesligamento = data;
            MotivoDesligamento = TextoHelper.Truncar(motivo, TamanhoMotivo);
        }
        else if (Situacao == SituacaoAluno.Desistente)
        {
            // Rematrícula: nova data de matrícula; o desligamento anterior fica no registro de alterações e nas matrículas encerradas.
            if (DataDesligamento is { } desligado && data < desligado)
                throw new ArgumentException("A rematrícula não pode ser antes do desligamento.", nameof(data));
            DataMatricula = data;
            DataDesligamento = null;
            MotivoDesligamento = null;
        }
        else if (data < SituacaoDesde)
        {
            throw new ArgumentException("A mudança não pode ser antes da situação atual.", nameof(data));
        }

        Situacao = para;
        SituacaoDesde = data;
    }

    /// <summary>Participa dos treinos e dos jogos (trial entra na chamada e na convocação — P20).</summary>
    public bool Treina => Situacao is SituacaoAluno.Trial or SituacaoAluno.Ativo;

    public bool TrialVencido(DateOnly hoje) => Situacao == SituacaoAluno.Trial && TrialAte is { } ate && ate < hoje;

    /// <summary>Dias até o fim do trial (0 = vence hoje; negativo = vencido). Nulo fora do trial.</summary>
    public int? DiasParaFimDoTrial(DateOnly hoje) =>
        Situacao == SituacaoAluno.Trial && TrialAte is { } ate ? ate.DayNumber - hoje.DayNumber : null;

    public int Idade(DateOnly hoje) => IdadeEm(DataNascimento, hoje);

    /// <summary>
    /// Idade em anos completos. Quem nasceu em 29/02 faz aniversário em 01/03 nos anos não bissextos
    /// (Código Civil, art. 132, § 3º: sem dia correspondente, o prazo vence no dia seguinte).
    /// </summary>
    public static int IdadeEm(DateOnly nascimento, DateOnly hoje)
    {
        var idade = hoje.Year - nascimento.Year;
        var aniversario = nascimento is { Month: 2, Day: 29 } && !DateTime.IsLeapYear(hoje.Year)
            ? new DateOnly(hoje.Year, 3, 1)
            : new DateOnly(hoje.Year, nascimento.Month, nascimento.Day);
        if (hoje < aniversario) idade--;
        return idade;
    }
}
