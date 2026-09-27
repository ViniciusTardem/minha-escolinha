using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Mensageria;

public enum CanalMensagem
{
    /// <summary>API oficial do WhatsApp: o canal de piso, para todos (BL-M09-02).</summary>
    WhatsApp = 10,

    /// <summary>Aviso no portal, para quem ativou o acesso (modo parcial, BL-M12-11). Não gasta crédito.</summary>
    Portal = 20
}

/// <summary>
/// A situação de uma mensagem (BL-M09-08). A entrega só avança: enviada → entregue → lida. Retida e suprimida são antes
/// do envio; falhou é o fim de uma tentativa que não chegou.
/// </summary>
public enum SituacaoMensagem
{
    /// <summary>Na fila do motor (outbox), talvez agendada para o fim da janela de silêncio.</summary>
    Pendente = 10,

    /// <summary>Essencial sem crédito ou sem canal: sai quando houver saldo (spec §8.2). Nunca é descartada em silêncio.</summary>
    Retida = 20,

    Enviada = 30,
    Entregue = 40,
    Lida = 50,
    Falhou = 60,

    /// <summary>Não saiu, com o motivo (sem crédito e não essencial, origem que deixou de valer, número fora do teste).</summary>
    Suprimida = 70
}

/// <summary>O que o motor reconhece como destinatário. Não existe aluno aqui (R4, BL-M09-13): por construção.</summary>
public enum TipoDestinatario
{
    Responsavel = 10,
    Professor = 20
}

public enum TipoEventoMensagem
{
    Criada = 10,
    Agendada = 20,
    Retida = 30,
    Liberada = 40,
    Enviada = 50,
    Entregue = 60,
    Lida = 70,
    Falhou = 80,
    Suprimida = 90,
    Tentativa = 100,
    Respondida = 110
}

/// <summary>A entrega que o canal devolve (webhook do WhatsApp).</summary>
public enum RetornoEntrega
{
    Enviada = 10,
    Entregue = 20,
    Lida = 30,
    Falhou = 40
}

/// <summary>
/// Uma mensagem do motor único (BL-M09-01): a fila de saída (outbox, gravada no mesmo SaveChanges da mudança de negócio)
/// e o log de entrega (BL-M09-08). Uma linha por destinatário e canal; a <see cref="Chave"/> impede a duplicidade.
/// Toda mensagem nasce de um gatilho do catálogo (R3, CA-20) e vai para um responsável ou professor, nunca para o aluno.
/// </summary>
public class Mensagem : IEntidadeDaEscolinha
{
    public const int TamanhoChave = 200;
    public const int TamanhoTipoOrigem = 40;
    public const int TamanhoNome = 150;
    public const int TamanhoTelefone = 20;
    public const int TamanhoTemplate = 120;
    public const int TamanhoConteudo = 2000;
    public const int TamanhoVariaveis = 4000;
    public const int TamanhoMotivo = 500;
    public const int TamanhoIdExterno = 150;

    public Guid Mensagem_ID { get; set; }
    public Guid Escolinha_ID { get; set; }

    public GatilhoMensagem Gatilho { get; private set; }
    public CanalMensagem Canal { get; private set; }

    /// <summary>Gatilho + origem + destinatário + canal: a mesma ocorrência não gera duas mensagens para a mesma pessoa.</summary>
    public string Chave { get; private set; } = string.Empty;

    /// <summary>De onde veio (ex.: "Sessao", "Convite") e o id do registro de origem.</summary>
    public string TipoOrigem { get; private set; } = string.Empty;
    public Guid Origem_ID { get; private set; }

    public TipoDestinatario TipoDestinatario { get; private set; }
    public Guid Destinatario_ID { get; private set; }
    public string DestinatarioNome { get; private set; } = string.Empty;

    /// <summary>E.164, no momento da criação (WhatsApp).</summary>
    public string? Telefone { get; private set; }

    /// <summary>A pessoa com acesso que vê o aviso no portal (canal Portal).</summary>
    public Guid? Pessoa_ID { get; private set; }

    /// <summary>O aluno de quem a mensagem fala, quando é um só (a ficha do aluno mostra as mensagens dele). Não é o destinatário.</summary>
    public Guid? Aluno_ID { get; private set; }

    /// <summary>Código do template (nome na Meta) e os parâmetros, em ordem, como texto JSON.</summary>
    public string Template { get; private set; } = string.Empty;
    public string Variaveis { get; private set; } = "[]";

    /// <summary>O texto como a pessoa lê (renderizado do template).</summary>
    public string Conteudo { get; private set; } = string.Empty;

    public SituacaoMensagem Situacao { get; private set; }
    public string? Motivo { get; private set; }

    public bool Essencial { get; private set; }
    public bool Urgente { get; private set; }

    /// <summary>Quando o compromisso de que a mensagem fala acontece (para saber se ainda é urgente no envio).</summary>
    public DateTime? Referencia { get; private set; }

    public DateTime CriadaEm { get; private set; }
    public DateTime? AgendadaPara { get; private set; }
    public DateTime? EnviadaEm { get; private set; }
    public DateTime? EntregueEm { get; private set; }
    public DateTime? LidaEm { get; private set; }
    public DateTime? FalhouEm { get; private set; }
    public DateTime? RespondidaEm { get; private set; }

    /// <summary>O id que o canal devolveu (wamid, no WhatsApp).</summary>
    public string? IdExterno { get; private set; }

    public int Tentativas { get; private set; }

    /// <summary>Crédito já debitado para esta mensagem (reserva atômica antes de chamar o canal).</summary>
    public bool CreditoReservado { get; private set; }

    public int CreditosConsumidos { get; private set; }

    /// <summary>
    /// Um trabalho está enviando esta mensagem até este horário. Com a versão de linha, dois trabalhos não enviam a
    /// mesma mensagem: o segundo encontra a reserva (ou perde a gravação) e desiste.
    /// </summary>
    public DateTime? ReservadaAte { get; private set; }

    /// <summary>Concorrência otimista (rowversion).</summary>
    public byte[] Versao { get; set; } = [];

    public List<EventoMensagem> Eventos { get; set; } = [];
    public List<RespostaMensagem> Respostas { get; set; } = [];

    public bool EmFila => Situacao is SituacaoMensagem.Pendente or SituacaoMensagem.Retida;

    public bool ReservadaPorOutro(DateTime agora) => ReservadaAte is { } ate && ate > agora;
    public bool Saiu => Situacao is SituacaoMensagem.Enviada or SituacaoMensagem.Entregue or SituacaoMensagem.Lida;

    public static string MontarChave(GatilhoMensagem gatilho, Guid origem, TipoDestinatario tipo, Guid destinatario, CanalMensagem canal) =>
        $"{(int)gatilho}:{origem:N}:{(int)tipo}:{destinatario:N}:{(int)canal}";

    /// <summary>
    /// Mensagem nova de um gatilho do catálogo. WhatsApp nasce pendente (o motor envia); o aviso do portal nasce enviado
    /// (disponível para quem abrir o portal) e não gasta crédito.
    /// </summary>
    public static Mensagem Nova(RegraGatilho regra, CanalMensagem canal, string tipoOrigem, Guid origemId,
        TipoDestinatario tipoDestinatario, Guid destinatarioId, string destinatarioNome, string? telefone, Guid? pessoaId, Guid? alunoId,
        string template, string variaveis, string conteudo, bool urgente, DateTime? referencia, DateTime agora)
    {
        if (canal == CanalMensagem.WhatsApp && string.IsNullOrWhiteSpace(telefone))
            throw new ArgumentException("Mensagem de WhatsApp sem telefone.", nameof(telefone));
        if (canal == CanalMensagem.Portal && pessoaId is null)
            throw new ArgumentException("Aviso de portal sem a pessoa que vai ver.", nameof(pessoaId));
        if (canal == CanalMensagem.Portal && !regra.AvisoNoPortal)
            throw new InvalidOperationException($"O gatilho {regra.Gatilho} não vira aviso no portal.");

        var mensagem = new Mensagem
        {
            Mensagem_ID = Guid.NewGuid(),
            Gatilho = regra.Gatilho,
            Canal = canal,
            Chave = MontarChave(regra.Gatilho, origemId, tipoDestinatario, destinatarioId, canal),
            TipoOrigem = TextoHelper.Truncar(tipoOrigem, TamanhoTipoOrigem) ?? string.Empty,
            Origem_ID = origemId,
            TipoDestinatario = tipoDestinatario,
            Destinatario_ID = destinatarioId,
            DestinatarioNome = TextoHelper.Truncar(destinatarioNome, TamanhoNome) ?? string.Empty,
            Telefone = canal == CanalMensagem.WhatsApp ? telefone : null,
            Pessoa_ID = pessoaId,
            Aluno_ID = alunoId,
            Template = TextoHelper.Truncar(template, TamanhoTemplate) ?? string.Empty,
            Variaveis = variaveis,
            Conteudo = TextoHelper.Truncar(conteudo, TamanhoConteudo) ?? string.Empty,
            Essencial = regra.Essencial,
            Urgente = urgente && regra.PodeSerUrgente,
            Referencia = referencia,
            CriadaEm = agora,
            Situacao = canal == CanalMensagem.Portal ? SituacaoMensagem.Enviada : SituacaoMensagem.Pendente
        };
        if (canal == CanalMensagem.Portal) mensagem.EnviadaEm = agora;
        mensagem.Registrar(TipoEventoMensagem.Criada, agora, canal == CanalMensagem.Portal ? "Aviso disponível no portal." : null);
        return mensagem;
    }

    /// <summary>Urgente de verdade no momento do envio: cancelamento ou alteração de compromisso nas próximas 24 horas.</summary>
    public bool UrgenteEm(DateTime agora) => Urgente && (Referencia is null || Referencia.Value <= agora.AddHours(24));

    public void Agendar(DateTime quando, string motivo, DateTime agora)
    {
        ExigirEmFila();
        ReservadaAte = null;
        AgendadaPara = quando;
        Registrar(TipoEventoMensagem.Agendada, agora, motivo);
    }

    public void Reter(string motivo, DateTime agora)
    {
        ExigirEmFila();
        ReservadaAte = null;
        if (Situacao == SituacaoMensagem.Retida && Motivo == motivo) return;
        Situacao = SituacaoMensagem.Retida;
        Motivo = TextoHelper.Truncar(motivo, TamanhoMotivo);
        Registrar(TipoEventoMensagem.Retida, agora, motivo);
    }

    /// <summary>Retida → pendente, quando o saldo volta.</summary>
    public void Liberar(string motivo, DateTime agora)
    {
        if (Situacao != SituacaoMensagem.Retida) return;
        Situacao = SituacaoMensagem.Pendente;
        Motivo = null;
        Registrar(TipoEventoMensagem.Liberada, agora, motivo);
    }

    public void Suprimir(string motivo, DateTime agora)
    {
        ExigirEmFila();
        ReservadaAte = null;
        Situacao = SituacaoMensagem.Suprimida;
        Motivo = TextoHelper.Truncar(motivo, TamanhoMotivo);
        Registrar(TipoEventoMensagem.Suprimida, agora, motivo);
    }

    public void ReservarCredito()
    {
        ExigirEmFila();
        CreditoReservado = true;
    }

    /// <summary>Marca que um trabalho está enviando (gravada antes de chamar o canal).</summary>
    public void ReservarEnvio(DateTime ate)
    {
        ExigirEmFila();
        ReservadaAte = ate;
    }

    /// <summary>Devolve se havia crédito reservado a estornar (e o solta).</summary>
    public bool SoltarCredito()
    {
        if (!CreditoReservado || CreditosConsumidos > 0) return false;
        CreditoReservado = false;
        return true;
    }

    public void RegistrarTentativa(string erro, DateTime agora)
    {
        ReservadaAte = null;
        Tentativas++;
        Motivo = TextoHelper.Truncar(erro, TamanhoMotivo);
        Registrar(TipoEventoMensagem.Tentativa, agora, erro);
    }

    public void MarcarEnviada(string? idExterno, DateTime agora)
    {
        ExigirEmFila();
        ReservadaAte = null;
        Tentativas++;
        Situacao = SituacaoMensagem.Enviada;
        Motivo = null;
        EnviadaEm = agora;
        IdExterno = TextoHelper.Truncar(idExterno, TamanhoIdExterno);
        CreditosConsumidos = CreditoReservado ? 1 : 0;
        Registrar(TipoEventoMensagem.Enviada, agora, null);
    }

    /// <summary>Falha definitiva antes de o canal aceitar a mensagem.</summary>
    public void Falhar(string motivo, DateTime agora)
    {
        ExigirEmFila();
        ReservadaAte = null;
        Tentativas++;
        Situacao = SituacaoMensagem.Falhou;
        Motivo = TextoHelper.Truncar(motivo, TamanhoMotivo);
        FalhouEm = agora;
        Registrar(TipoEventoMensagem.Falhou, agora, motivo);
    }

    /// <summary>
    /// A entrega que o canal devolveu. Só avança (enviada → entregue → lida): um retorno atrasado não desfaz o que já
    /// aconteceu. A falha depois de enviada vale enquanto a mensagem não foi lida. Devolve se algo mudou.
    /// </summary>
    public bool AtualizarEntrega(RetornoEntrega retorno, DateTime em, string? detalhe, DateTime agora)
    {
        if (!Saiu && Situacao != SituacaoMensagem.Falhou) return false;

        switch (retorno)
        {
            case RetornoEntrega.Enviada:
                return false;
            case RetornoEntrega.Entregue:
                if (EntregueEm is not null || Situacao == SituacaoMensagem.Falhou) return false;
                EntregueEm = em;
                if (Situacao == SituacaoMensagem.Enviada) Situacao = SituacaoMensagem.Entregue;
                Registrar(TipoEventoMensagem.Entregue, agora, null, em);
                return true;
            case RetornoEntrega.Lida:
                if (LidaEm is not null || Situacao == SituacaoMensagem.Falhou) return false;
                LidaEm = em;
                EntregueEm ??= em;
                Situacao = SituacaoMensagem.Lida;
                Registrar(TipoEventoMensagem.Lida, agora, null, em);
                return true;
            case RetornoEntrega.Falhou:
                if (Situacao is SituacaoMensagem.Lida or SituacaoMensagem.Falhou) return false;
                Situacao = SituacaoMensagem.Falhou;
                FalhouEm = em;
                Motivo = TextoHelper.Truncar(detalhe ?? "O WhatsApp não entregou a mensagem.", TamanhoMotivo);
                Registrar(TipoEventoMensagem.Falhou, agora, Motivo, em);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Tira o segredo do link do conteúdo guardado (convite): depois do envio, o banco não precisa dele.</summary>
    public void MascararTrecho(string trecho, string mascara)
    {
        if (string.IsNullOrEmpty(trecho)) return;
        Conteudo = Conteudo.Replace(trecho, mascara, StringComparison.Ordinal);
        Variaveis = Variaveis.Replace(trecho, mascara, StringComparison.Ordinal);
    }

    /// <summary>
    /// O aviso do portal que ninguém abriu ainda sai do portal quando a origem deixa de valer (regra de ouro 5: a falta
    /// que disparou o alerta foi corrigida). O aviso já lido fica, como a mensagem de WhatsApp que já saiu.
    /// </summary>
    public bool RecolherDoPortal(string motivo, DateTime agora)
    {
        if (Canal != CanalMensagem.Portal || LidaEm is not null || Situacao == SituacaoMensagem.Suprimida) return false;
        Situacao = SituacaoMensagem.Suprimida;
        Motivo = TextoHelper.Truncar(motivo, TamanhoMotivo);
        Registrar(TipoEventoMensagem.Suprimida, agora, motivo);
        return true;
    }

    /// <summary>Aviso do portal aberto por quem recebeu (captura de leitura pelo portal, BL-M09-07).</summary>
    public bool MarcarLidaNoPortal(DateTime agora)
    {
        if (Canal != CanalMensagem.Portal || LidaEm is not null) return false;
        LidaEm = agora;
        EntregueEm ??= agora;
        Situacao = SituacaoMensagem.Lida;
        Registrar(TipoEventoMensagem.Lida, agora, "Aberto no portal.");
        return true;
    }

    /// <summary>Resposta que chegou (botão ou texto). Quem trata é o módulo de origem, quando o gatilho espera resposta.</summary>
    public RespostaMensagem RegistrarResposta(CanalMensagem canal, string? texto, string? payload, string? idExterno, DateTime recebidaEm,
        DateTime agora)
    {
        // Sem id: o banco gera (NEWSEQUENTIALID). Com o id preenchido, o EF acharia pela coleção uma linha "existente" (CLAUDE.md §3).
        var resposta = new RespostaMensagem
        {
            Escolinha_ID = Escolinha_ID,
            Mensagem_ID = Mensagem_ID,
            Canal = canal,
            Texto = TextoHelper.Truncar(texto, RespostaMensagem.TamanhoTexto),
            Payload = TextoHelper.Truncar(payload, RespostaMensagem.TamanhoPayload),
            IdExterno = TextoHelper.Truncar(idExterno, TamanhoIdExterno),
            RecebidaEm = recebidaEm
        };
        Respostas.Add(resposta);
        RespondidaEm ??= recebidaEm;
        if (Canal == CanalMensagem.WhatsApp && Saiu && LidaEm is null)
        {
            // Quem respondeu leu: o recibo de leitura pode não vir (a pessoa desligou a confirmação de leitura).
            LidaEm = recebidaEm;
            EntregueEm ??= recebidaEm;
            Situacao = SituacaoMensagem.Lida;
        }
        Registrar(TipoEventoMensagem.Respondida, agora, payload is not null ? $"Botão: {texto ?? payload}" : texto, recebidaEm);
        return resposta;
    }

    private void ExigirEmFila()
    {
        if (!EmFila) throw new InvalidOperationException($"A mensagem já saiu da fila ({Situacao}).");
    }

    /// <summary>
    /// A linha do tempo só cresce. O evento nasce sem id: o banco gera (NEWSEQUENTIALID), e o EF, achando a linha nova pela
    /// coleção de uma mensagem já rastreada, sabe que é inclusão. Com o id preenchido, ele faria UPDATE (CLAUDE.md §3).
    /// </summary>
    private void Registrar(TipoEventoMensagem tipo, DateTime agora, string? detalhe, DateTime? em = null) =>
        Eventos.Add(new EventoMensagem
        {
            Escolinha_ID = Escolinha_ID,
            Mensagem_ID = Mensagem_ID,
            Tipo = tipo,
            Em = em ?? agora,
            Detalhe = TextoHelper.Truncar(detalhe, EventoMensagem.TamanhoDetalhe)
        });

    public static string RotuloSituacao(SituacaoMensagem situacao) => situacao switch
    {
        SituacaoMensagem.Pendente => "Na fila",
        SituacaoMensagem.Retida => "Retida",
        SituacaoMensagem.Enviada => "Enviada",
        SituacaoMensagem.Entregue => "Entregue",
        SituacaoMensagem.Lida => "Lida",
        SituacaoMensagem.Falhou => "Falhou",
        SituacaoMensagem.Suprimida => "Suprimida",
        _ => situacao.ToString()
    };
}

/// <summary>Um passo da linha do tempo da mensagem (criada, agendada, retida, enviada, entregue, lida, respondida...).</summary>
public class EventoMensagem : IEntidadeDaEscolinha
{
    public const int TamanhoDetalhe = 500;

    public Guid EventoMensagem_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Mensagem_ID { get; set; }
    public TipoEventoMensagem Tipo { get; set; }
    public DateTime Em { get; set; }
    public string? Detalhe { get; set; }

    public static string Rotulo(TipoEventoMensagem tipo) => tipo switch
    {
        TipoEventoMensagem.Criada => "Criada",
        TipoEventoMensagem.Agendada => "Agendada",
        TipoEventoMensagem.Retida => "Retida",
        TipoEventoMensagem.Liberada => "Liberada",
        TipoEventoMensagem.Enviada => "Enviada",
        TipoEventoMensagem.Entregue => "Entregue",
        TipoEventoMensagem.Lida => "Lida",
        TipoEventoMensagem.Falhou => "Falhou",
        TipoEventoMensagem.Suprimida => "Suprimida",
        TipoEventoMensagem.Tentativa => "Tentativa sem sucesso",
        TipoEventoMensagem.Respondida => "Resposta recebida",
        _ => tipo.ToString()
    };
}

/// <summary>
/// Resposta que chegou pelo WhatsApp ou pelo portal (BL-M09-07). Botão traz o payload, que o módulo de origem entende;
/// texto livre fica registrado no log e não muda estado nenhum.
/// </summary>
public class RespostaMensagem : IEntidadeDaEscolinha
{
    public const int TamanhoTexto = 1000;
    public const int TamanhoPayload = 200;
    public const int TamanhoObservacao = 300;

    public Guid RespostaMensagem_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Mensagem_ID { get; set; }
    public CanalMensagem Canal { get; set; }
    public string? Texto { get; set; }
    public string? Payload { get; set; }
    public string? IdExterno { get; set; }
    public DateTime RecebidaEm { get; set; }

    /// <summary>O módulo de origem tratou a resposta (ex.: a convocação mudou de estado, S5).</summary>
    public bool Tratada { get; set; }

    public string? Observacao { get; set; }
}
