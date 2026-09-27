using System.Globalization;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Domain.Presencas;

/// <summary>A presença de um aluno numa sessão (plano §4.2). Sem registro, o aluno está "não marcado".</summary>
public enum SituacaoPresenca
{
    Presente = 10,
    Falta = 20,

    /// <summary>Falta com motivo. Conta em separado e não dispara o alerta, a menos que a escolinha ligue (P04).</summary>
    Justificada = 30
}

/// <summary>
/// A presença de um aluno numa sessão de treino (BL-M05-01): uma linha por sessão e aluno, com a última marcação
/// aplicada. Quem decide se uma marcação vale é a <see cref="RegraMarcacao"/> (o registro da quadra prevalece, R10).
/// O motivo de saúde é dado sensível (R5, BL-M05-13): quem não pode ver a saúde do aluno vê só "falta justificada".
/// </summary>
public class Presenca : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoJustificativa = 300;
    public const int TamanhoDispositivo = 64;
    public const int TamanhoNome = 150;

    public Guid Presenca_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid SessaoTreino_ID { get; set; }
    public Guid Aluno_ID { get; set; }

    /// <summary>A turma da sessão, copiada para contar as faltas do aluno na turma sem juntar com a sessão.</summary>
    public Guid Turma_ID { get; set; }

    /// <summary>O início da sessão (Brasília), copiado pelo mesmo motivo: o mês e a sequência das faltas.</summary>
    public DateTime SessaoInicio { get; set; }

    public SituacaoPresenca Situacao { get; private set; }
    public string? Justificativa { get; private set; }
    public bool JustificativaSaude { get; private set; }

    /// <summary>A última marcação aplicada: o aparelho, a ordem dele e o id (UUID) que o aparelho gerou.</summary>
    public string Dispositivo { get; private set; } = string.Empty;
    public long Sequencia { get; private set; }
    public Guid Marcacao { get; private set; }

    /// <summary>Quando foi marcada no aparelho (Brasília), e por quem.</summary>
    public DateTime MarcadaEm { get; private set; }
    public Guid? MarcadaPor_ID { get; private set; }
    public string MarcadaPorNome { get; private set; } = string.Empty;

    public byte[] Versao { get; set; } = [];

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    public static Presenca Nova(SessaoTreino sessao, Guid alunoId) => new()
    {
        Presenca_ID = Guid.NewGuid(),
        SessaoTreino_ID = sessao.SessaoTreino_ID,
        Aluno_ID = alunoId,
        Turma_ID = sessao.Turma_ID,
        SessaoInicio = sessao.Inicio
    };

    public void Aplicar(NovaMarcacao m, Guid? autorId, string autorNome)
    {
        Situacao = m.Situacao;
        var justificada = m.Situacao == SituacaoPresenca.Justificada;
        Justificativa = justificada ? TextoHelper.Truncar(m.Justificativa?.Trim(), TamanhoJustificativa) is { Length: > 0 } texto ? texto : null : null;
        JustificativaSaude = justificada && m.Saude;
        Dispositivo = TextoHelper.Truncar(m.Dispositivo, TamanhoDispositivo) ?? string.Empty;
        Sequencia = m.Sequencia;
        Marcacao = m.Id;
        MarcadaEm = m.MarcadaEm;
        MarcadaPor_ID = autorId;
        MarcadaPorNome = TextoHelper.Truncar(autorNome, TamanhoNome) ?? string.Empty;
    }

    public static string Rotulo(SituacaoPresenca situacao) => situacao switch
    {
        SituacaoPresenca.Presente => "Presente",
        SituacaoPresenca.Falta => "Falta",
        SituacaoPresenca.Justificada => "Falta justificada",
        _ => situacao.ToString()
    };
}

/// <summary>Uma marcação que chegou do aparelho (online ou da fila guardada sem sinal).</summary>
public sealed record NovaMarcacao(
    Guid Id,
    Guid SessaoId,
    Guid AlunoId,
    SituacaoPresenca Situacao,
    string? Justificativa,
    bool Saude,
    string Dispositivo,
    long Sequencia,
    DateTime MarcadaEm);

public enum ResultadoMarcacao
{
    Aplicada = 10,

    /// <summary>Uma marcação mais nova do mesmo aparelho já valia (reenvio fora de ordem).</summary>
    Ignorada = 20
}

/// <summary>
/// O registro de cada marcação recebida (aplicada ou ignorada). O id que o aparelho gera é único: o reenvio da mesma
/// marcação é reconhecido e não refaz nada, então um reenvio atrasado nunca desfaz uma correção feita depois (CA-21).
/// O texto do motivo de saúde não é copiado para cá (R5): fica só na presença.
/// </summary>
public class MarcacaoPresenca : IEntidadeDaEscolinha
{
    public const int TamanhoObservacao = 200;

    public Guid MarcacaoPresenca_ID { get; set; }
    public Guid Escolinha_ID { get; set; }

    /// <summary>O id (UUID) gerado no aparelho.</summary>
    public Guid Marcacao { get; set; }

    public Guid SessaoTreino_ID { get; set; }
    public Guid Aluno_ID { get; set; }
    public SituacaoPresenca Situacao { get; set; }
    public string? Justificativa { get; set; }
    public bool JustificativaSaude { get; set; }
    public string Dispositivo { get; set; } = string.Empty;
    public long Sequencia { get; set; }
    public DateTime MarcadaEm { get; set; }
    public DateTime RecebidaEm { get; set; }
    public Guid? Pessoa_ID { get; set; }
    public string PessoaNome { get; set; } = string.Empty;
    public ResultadoMarcacao Resultado { get; set; }
    public string? Observacao { get; set; }

    public static MarcacaoPresenca De(NovaMarcacao m, ResultadoMarcacao resultado, string? observacao, DateTime recebidaEm,
        Guid? pessoaId, string pessoaNome) => new()
    {
        Marcacao = m.Id,
        SessaoTreino_ID = m.SessaoId,
        Aluno_ID = m.AlunoId,
        Situacao = m.Situacao,
        JustificativaSaude = m.Situacao == SituacaoPresenca.Justificada && m.Saude,
        Justificativa = m.Situacao == SituacaoPresenca.Justificada && !m.Saude
            ? TextoHelper.Truncar(m.Justificativa?.Trim(), Presenca.TamanhoJustificativa)
            : null,
        Dispositivo = TextoHelper.Truncar(m.Dispositivo, Presenca.TamanhoDispositivo) ?? string.Empty,
        Sequencia = m.Sequencia,
        MarcadaEm = m.MarcadaEm,
        RecebidaEm = recebidaEm,
        Pessoa_ID = pessoaId,
        PessoaNome = TextoHelper.Truncar(pessoaNome, Presenca.TamanhoNome) ?? string.Empty,
        Resultado = resultado,
        Observacao = TextoHelper.Truncar(observacao, TamanhoObservacao)
    };
}

public enum SituacaoAlertaFaltas
{
    /// <summary>A regra foi atingida e a mensagem entrou na fila do motor para os responsáveis.</summary>
    Disparado = 10,

    /// <summary>A regra foi atingida dentro da janela anti-duplicidade: nada sai, e a supressão fica registrada (BL-M05-09).</summary>
    Suprimido = 20,

    /// <summary>A falta que atingiu a regra foi corrigida antes de qualquer mensagem sair.</summary>
    Anulado = 30
}

/// <summary>
/// Cada vez que a regra de faltas é atingida (BL-M05-08): disparado, suprimido pela janela anti-duplicidade ou anulado.
/// As mensagens do alerta têm esta linha como origem: a ficha do aluno mostra qual alerta saiu, quando e para quem (CA-6).
/// A regra, o limite e a janela são os do momento da decisão (regra de ouro 14).
/// </summary>
public class AlertaFaltas : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoPeriodo = 60;
    public const int TamanhoMotivo = 300;

    public Guid AlertaFaltas_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Aluno_ID { get; set; }
    public Guid Turma_ID { get; set; }

    /// <summary>A sessão da falta avaliada quando a regra foi atingida.</summary>
    public Guid SessaoTreino_ID { get; set; }

    public DateTime SessaoInicio { get; set; }
    public RegraFaltas Regra { get; set; }
    public int Limite { get; set; }

    /// <summary>Se a falta justificada contava na decisão (P04): a revalidação no envio usa o mesmo critério.</summary>
    public bool ContaJustificada { get; set; }

    public int Faltas { get; set; }

    /// <summary>"setembro" (no mês) ou "sequência" (seguidas): vai no texto da mensagem.</summary>
    public string Periodo { get; set; } = string.Empty;

    public SituacaoAlertaFaltas Situacao { get; private set; }
    public string? Motivo { get; private set; }
    public DateTime AvaliadoEm { get; set; }
    public DateTime? AnuladoEm { get; private set; }

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    public static AlertaFaltas Novo(Presenca falta, RegraFaltas regra, int limite, bool contaJustificada, RegraAtingida atingida, DateTime agora,
        DateTime? ultimoDisparo, int janelaDias)
    {
        var alerta = new AlertaFaltas
        {
            AlertaFaltas_ID = Guid.NewGuid(),
            Aluno_ID = falta.Aluno_ID,
            Turma_ID = falta.Turma_ID,
            SessaoTreino_ID = falta.SessaoTreino_ID,
            SessaoInicio = falta.SessaoInicio,
            Regra = regra,
            Limite = limite,
            ContaJustificada = contaJustificada,
            Faltas = atingida.Faltas,
            Periodo = TextoHelper.Truncar(atingida.Periodo, TamanhoPeriodo) ?? string.Empty,
            AvaliadoEm = agora,
            Situacao = SituacaoAlertaFaltas.Disparado
        };
        if (AvaliadorFaltas.DentroDaJanela(ultimoDisparo, agora, janelaDias))
        {
            alerta.Situacao = SituacaoAlertaFaltas.Suprimido;
            var quando = ultimoDisparo!.Value.ToString("dd'/'MM'/'yyyy HH':'mm", CultureInfo.InvariantCulture);
            alerta.Motivo = $"Dentro da janela anti-duplicidade de {janelaDias} dia(s): o último alerta deste aluno saiu em {quando}.";
        }
        return alerta;
    }

    public void Anular(string motivo, DateTime agora)
    {
        if (Situacao != SituacaoAlertaFaltas.Disparado) return;
        Situacao = SituacaoAlertaFaltas.Anulado;
        Motivo = TextoHelper.Truncar(motivo, TamanhoMotivo);
        AnuladoEm = agora;
    }

    /// <summary>"1 falta" ou "3 faltas": vai no texto da mensagem, que não pode flexionar a palavra sozinho.</summary>
    public string QuantidadeFaltas => $"{Faltas} falta{(Faltas == 1 ? "" : "s")}";

    /// <summary>"3 faltas em setembro" ou "3 faltas em sequência".</summary>
    public string Descricao => $"{QuantidadeFaltas} em {Periodo}";

    public static string Rotulo(SituacaoAlertaFaltas situacao) => situacao switch
    {
        SituacaoAlertaFaltas.Disparado => "Disparado",
        SituacaoAlertaFaltas.Suprimido => "Suprimido",
        SituacaoAlertaFaltas.Anulado => "Anulado",
        _ => situacao.ToString()
    };
}
