using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Eventos;

/// <summary>
/// Máquina da convocação, por convocado (spec §6): enviada → confirmada | recusada | sem resposta | cancelada. Antes do
/// disparo, a convocação fica "selecionada": é o rascunho da montagem do fim de semana (BL-M07-08), que só vira mensagem
/// no disparo.
/// </summary>
public enum SituacaoConvocacao
{
    /// <summary>Escolhida na montagem, ainda não disparada: nenhuma mensagem saiu.</summary>
    Selecionada = 5,

    Enviada = 10,
    Confirmada = 20,
    Recusada = 30,

    /// <summary>Ninguém respondeu até o evento começar (P19).</summary>
    SemResposta = 40,

    Cancelada = 50
}

/// <summary>De onde veio a resposta (spec §5): o app, o WhatsApp ou o registro do professor ou do gestor.</summary>
public enum OrigemResposta
{
    WhatsApp = 10,
    Portal = 20,
    RegistroManual = 30
}

/// <summary>O que a resposta da família fez.</summary>
public enum ResultadoResposta
{
    /// <summary>Era a primeira: a convocação passou a confirmada ou recusada.</summary>
    Aplicada = 10,

    /// <summary>Já havia resposta (vale a primeira, P03): fica no histórico, e a diferente fica visível para quem convocou.</summary>
    Posterior = 20,

    /// <summary>Chegou depois do início do evento (P19): fica no histórico e visível.</summary>
    Tardia = 30,

    /// <summary>A convocação foi cancelada: só o histórico.</summary>
    Ignorada = 40
}

public enum TipoHistoricoConvocacao
{
    Selecionada = 10,
    Enviada = 20,
    Lembrete = 30,
    Resposta = 40,
    RespostaPosterior = 50,
    RespostaTardia = 60,
    RegistroManual = 70,
    SemResposta = 80,
    Cancelada = 90
}

/// <summary>
/// A chamada de um aluno para um evento (BL-M07-05 a BL-M07-13): uma por convocado, não uma por jogo. Guarda o estado
/// da confirmação daquele convocado, a origem da resposta, quem respondeu e quando, e o histórico (lembretes, respostas,
/// registros por fora).
/// <para>
/// Dois responsáveis podem responder (P03): vale a primeira resposta; a seguinte, se diferente, fica em
/// <see cref="OutraResposta"/> para o professor decidir. O professor (ou o gestor) sobrescreve qualquer estado,
/// registrando que a resposta veio por fora (§6).
/// </para>
/// </summary>
public class Convocacao : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoNome = 150;
    public const int TamanhoOutraResposta = 200;
    public const int TamanhoMotivo = 300;

    public Guid Convocacao_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Evento_ID { get; set; }
    public Guid Aluno_ID { get; set; }

    /// <summary>A turma pela qual o aluno foi convocado (a do evento em que ele está matriculado).</summary>
    public Guid Turma_ID { get; set; }

    public SituacaoConvocacao Situacao { get; private set; } = SituacaoConvocacao.Selecionada;
    public DateTime? EnviadaEm { get; private set; }
    public DateTime? LembreteEm { get; private set; }
    public DateTime? RespondidaEm { get; private set; }
    public OrigemResposta? Origem { get; private set; }

    /// <summary>Quem respondeu: o responsável (WhatsApp, portal) ou quem registrou por fora.</summary>
    public string? RespondidoPorNome { get; private set; }

    public Guid? RespondidoPor_ID { get; private set; }

    /// <summary>Resposta que chegou depois da primeira (e diferente dela) ou depois do início: o professor decide.</summary>
    public string? OutraResposta { get; private set; }

    public string? MotivoCancelamento { get; private set; }

    public List<HistoricoConvocacao> Historico { get; set; } = [];

    public byte[] Versao { get; set; } = [];

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    /// <summary>Enviada e ainda sem resposta: recebe o lembrete e vira "sem resposta" no início do evento.</summary>
    public bool Pendente => Situacao == SituacaoConvocacao.Enviada;

    /// <summary>Conta como "convocado" para o alerta de dois jogos ao mesmo tempo (BL-M07-06).</summary>
    public bool Ativa => Situacao is SituacaoConvocacao.Selecionada or SituacaoConvocacao.Enviada or SituacaoConvocacao.Confirmada
        or SituacaoConvocacao.SemResposta;

    /// <summary>Já saiu mensagem para a família (tudo, menos a selecionada).</summary>
    public bool Disparada => Situacao != SituacaoConvocacao.Selecionada;

    public static Convocacao Selecionar(Guid eventoId, Guid alunoId, Guid turmaId, DateTime agora, string autor)
    {
        var convocacao = new Convocacao
        {
            Convocacao_ID = Guid.NewGuid(),
            Evento_ID = eventoId,
            Aluno_ID = alunoId,
            Turma_ID = turmaId,
            Situacao = SituacaoConvocacao.Selecionada
        };
        convocacao.Registrar(TipoHistoricoConvocacao.Selecionada, agora, $"Por {autor}.");
        return convocacao;
    }

    public void Disparar(DateTime agora)
    {
        if (Situacao != SituacaoConvocacao.Selecionada) throw new InvalidOperationException("Só a convocação selecionada é disparada.");
        Situacao = SituacaoConvocacao.Enviada;
        EnviadaEm = agora;
        Registrar(TipoHistoricoConvocacao.Enviada, agora, null);
    }

    /// <summary>A resposta da família (WhatsApp ou portal). Vale a primeira (P03).</summary>
    public ResultadoResposta Responder(bool vai, OrigemResposta origem, string quem, Guid? quemId, DateTime em, DateTime agora)
    {
        if (origem == OrigemResposta.RegistroManual) throw new ArgumentException("O registro por fora usa RegistrarPorFora.", nameof(origem));
        var texto = $"{RotuloResposta(vai)} · {quem} ({Rotulo(origem)})";
        switch (Situacao)
        {
            case SituacaoConvocacao.Enviada:
                Aplicar(vai, origem, quem, quemId, em);
                Registrar(TipoHistoricoConvocacao.Resposta, agora, texto, em);
                return ResultadoResposta.Aplicada;
            case SituacaoConvocacao.Confirmada or SituacaoConvocacao.Recusada:
                Registrar(TipoHistoricoConvocacao.RespostaPosterior, agora, texto, em);
                if (vai != (Situacao == SituacaoConvocacao.Confirmada))
                    OutraResposta = TextoHelper.Truncar($"{RotuloResposta(vai)} · {quem}, {em:dd/MM HH:mm}", TamanhoOutraResposta);
                return ResultadoResposta.Posterior;
            case SituacaoConvocacao.SemResposta:
                Registrar(TipoHistoricoConvocacao.RespostaTardia, agora, texto, em);
                OutraResposta = TextoHelper.Truncar($"{RotuloResposta(vai)} · {quem}, {em:dd/MM HH:mm} (depois do início)", TamanhoOutraResposta);
                return ResultadoResposta.Tardia;
            default:
                Registrar(TipoHistoricoConvocacao.RespostaPosterior, agora, $"{texto}: a convocação não estava valendo.", em);
                return ResultadoResposta.Ignorada;
        }
    }

    public bool PodeRegistrarPorFora => Situacao is SituacaoConvocacao.Enviada or SituacaoConvocacao.Confirmada
        or SituacaoConvocacao.Recusada or SituacaoConvocacao.SemResposta;

    /// <summary>A resposta que veio por fora, registrada pelo professor ou pelo gestor: sobrescreve qualquer estado (§6).</summary>
    public void RegistrarPorFora(bool vai, string autor, Guid? autorId, DateTime agora)
    {
        if (!PodeRegistrarPorFora)
            throw new InvalidOperationException($"Convocação {Rotulo(Situacao).ToLowerInvariant()} não recebe resposta.");
        Aplicar(vai, OrigemResposta.RegistroManual, autor, autorId, agora);
        Registrar(TipoHistoricoConvocacao.RegistroManual, agora, $"{RotuloResposta(vai)} · registrado por {autor}.");
    }

    public void RegistrarLembrete(DateTime agora)
    {
        LembreteEm = agora;
        Registrar(TipoHistoricoConvocacao.Lembrete, agora, null);
    }

    /// <summary>O evento começou e ninguém respondeu (P19).</summary>
    public bool MarcarSemResposta(DateTime agora)
    {
        if (Situacao != SituacaoConvocacao.Enviada) return false;
        Situacao = SituacaoConvocacao.SemResposta;
        Registrar(TipoHistoricoConvocacao.SemResposta, agora, null);
        return true;
    }

    /// <summary>Evento cancelado ou adiado, aluno desligado: a convocação disparada fica no histórico como cancelada.</summary>
    public bool Cancelar(string motivo, DateTime agora)
    {
        if (!Disparada || Situacao == SituacaoConvocacao.Cancelada) return false;
        Situacao = SituacaoConvocacao.Cancelada;
        MotivoCancelamento = TextoHelper.Truncar(motivo, TamanhoMotivo);
        Registrar(TipoHistoricoConvocacao.Cancelada, agora, motivo);
        return true;
    }

    private void Aplicar(bool vai, OrigemResposta origem, string quem, Guid? quemId, DateTime em)
    {
        Situacao = vai ? SituacaoConvocacao.Confirmada : SituacaoConvocacao.Recusada;
        RespondidaEm = em;
        Origem = origem;
        RespondidoPorNome = TextoHelper.Truncar(quem, TamanhoNome);
        RespondidoPor_ID = quemId;
        OutraResposta = null;
    }

    private void Registrar(TipoHistoricoConvocacao tipo, DateTime agora, string? detalhe, DateTime? em = null) =>
        Historico.Add(new HistoricoConvocacao
        {
            Tipo = tipo,
            Em = em ?? agora,
            Detalhe = TextoHelper.Truncar(detalhe, HistoricoConvocacao.TamanhoDetalhe)
        });

    public static string RotuloResposta(bool vai) => vai ? "Vai" : "Não vai";

    public static string Rotulo(SituacaoConvocacao situacao) => situacao switch
    {
        SituacaoConvocacao.Selecionada => "Não enviada",
        SituacaoConvocacao.Enviada => "Aguardando resposta",
        SituacaoConvocacao.Confirmada => "Confirmada",
        SituacaoConvocacao.Recusada => "Recusada",
        SituacaoConvocacao.SemResposta => "Sem resposta",
        SituacaoConvocacao.Cancelada => "Cancelada",
        _ => situacao.ToString()
    };

    public static string Rotulo(OrigemResposta origem) => origem switch
    {
        OrigemResposta.WhatsApp => "WhatsApp",
        OrigemResposta.Portal => "portal",
        OrigemResposta.RegistroManual => "registrado por fora",
        _ => origem.ToString()
    };
}

/// <summary>Uma linha do histórico da convocação. A chave nasce no banco: o filho novo entra pela coleção (D56).</summary>
public class HistoricoConvocacao : IEntidadeDaEscolinha
{
    public const int TamanhoDetalhe = 300;

    public Guid HistoricoConvocacao_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Convocacao_ID { get; set; }
    public TipoHistoricoConvocacao Tipo { get; set; }
    public DateTime Em { get; set; }
    public string? Detalhe { get; set; }

    public static string Rotulo(TipoHistoricoConvocacao tipo) => tipo switch
    {
        TipoHistoricoConvocacao.Selecionada => "Selecionada",
        TipoHistoricoConvocacao.Enviada => "Enviada",
        TipoHistoricoConvocacao.Lembrete => "Lembrete",
        TipoHistoricoConvocacao.Resposta => "Resposta",
        TipoHistoricoConvocacao.RespostaPosterior => "Outra resposta",
        TipoHistoricoConvocacao.RespostaTardia => "Resposta depois do início",
        TipoHistoricoConvocacao.RegistroManual => "Registrada por fora",
        TipoHistoricoConvocacao.SemResposta => "Sem resposta",
        TipoHistoricoConvocacao.Cancelada => "Cancelada",
        _ => tipo.ToString()
    };
}
