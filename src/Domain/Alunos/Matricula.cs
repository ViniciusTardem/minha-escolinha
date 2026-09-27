using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Domain.Alunos;

public enum SituacaoMatricula
{
    Ativa = 10,
    Encerrada = 20
}

/// <summary>
/// Matrícula do aluno numa turma (BL-M03-04): uma por turma, com mensalidade e datas próprias — o aluno pode estar
/// em mais de uma turma. É a base da recorrência de cobrança (S4): matrícula encerrada não gera cobrança depois do fim.
/// A matrícula ativa ocupa uma vaga da turma (BL-M04-03); encerrar libera a vaga e preserva o período no histórico (CA-3).
/// </summary>
public class Matricula : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoMotivo = 300;

    public Guid Matricula_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Aluno_ID { get; set; }
    public Aluno? Aluno { get; set; }
    public Guid Turma_ID { get; set; }
    public Turma? Turma { get; set; }

    public DateOnly DataInicio { get; set; }

    /// <summary>Último dia na turma (inclusive).</summary>
    public DateOnly? DataFim { get; private set; }

    /// <summary>Nasce com a mensalidade da turma; valor diferente (desconto, bolsa) é ação sensível registrada.</summary>
    public decimal Mensalidade { get; set; }

    public SituacaoMatricula Situacao { get; private set; } = SituacaoMatricula.Ativa;
    public string? MotivoEncerramento { get; private set; }

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    public bool Ativa => Situacao == SituacaoMatricula.Ativa;

    public void Encerrar(DateOnly data, string motivo)
    {
        if (!Ativa) throw new InvalidOperationException("A matrícula já está encerrada.");
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("O encerramento exige motivo.", nameof(motivo));
        if (data < DataInicio) throw new ArgumentException("A matrícula não pode terminar antes de começar.", nameof(data));
        Situacao = SituacaoMatricula.Encerrada;
        DataFim = data;
        MotivoEncerramento = TextoHelper.Truncar(motivo, TamanhoMotivo);
    }

    /// <summary>O aluno fazia parte da turma nesta data (o histórico do período em que esteve ativo, CA-3).</summary>
    public bool VigenteEm(DateOnly data) => DataInicio <= data && (DataFim is null || data <= DataFim);
}
