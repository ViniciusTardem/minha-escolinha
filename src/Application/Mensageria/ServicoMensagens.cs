using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Application.Mensageria;

public sealed record MensagemNaLista(
    Guid Id,
    DateTime CriadaEm,
    GatilhoMensagem Gatilho,
    CanalMensagem Canal,
    string Destinatario,
    TipoDestinatario TipoDestinatario,
    SituacaoMensagem Situacao,
    string? Motivo,
    DateTime? AgendadaPara,
    bool Respondida,
    int Creditos);

public sealed record EventoDaMensagem(TipoEventoMensagem Tipo, DateTime Em, string? Detalhe);

public sealed record RespostaDaMensagem(DateTime RecebidaEm, CanalMensagem Canal, string? Texto, bool Botao, bool Tratada, string? Observacao);

public sealed record DetalheMensagem(
    Guid Id,
    GatilhoMensagem Gatilho,
    CanalMensagem Canal,
    string Destinatario,
    TipoDestinatario TipoDestinatario,
    Guid DestinatarioId,
    string? Telefone,
    Guid? AlunoId,
    string Template,
    string Conteudo,
    SituacaoMensagem Situacao,
    string? Motivo,
    bool Essencial,
    bool Urgente,
    DateTime CriadaEm,
    DateTime? AgendadaPara,
    DateTime? EnviadaEm,
    DateTime? EntregueEm,
    DateTime? LidaEm,
    int Tentativas,
    int Creditos,
    IReadOnlyList<EventoDaMensagem> Linha,
    IReadOnlyList<RespostaDaMensagem> Respostas);

/// <summary>O quadro do motor para o gestor: contagens do período, créditos e o que pede atenção.</summary>
public sealed record ResumoMensageria(
    int Saldo,
    int AvisoSaldoAbaixoDe,
    int Retidas,
    IReadOnlyDictionary<SituacaoMensagem, int> PorSituacao,
    string Canal,
    bool CanalHabilitado)
{
    public bool SaldoBaixo => Saldo < AvisoSaldoAbaixoDe;
    public int Total => PorSituacao.Values.Sum();
    public int Contar(SituacaoMensagem situacao) => PorSituacao.GetValueOrDefault(situacao);
}

public sealed record AvisoDoPortal(Guid Id, DateTime Em, GatilhoMensagem Gatilho, string Conteudo, bool NovoAgora);

/// <summary>
/// O log de entrega consultável (BL-M09-08) e os avisos do portal (o segundo canal, BL-M09-02). O log é do gestor: quem
/// configura a escolinha vê toda mensagem, com o motivo de cada retenção ou supressão e a linha do tempo.
/// </summary>
public class ServicoMensagens(
    IMensageriaRepositorio repositorio,
    IConfiguracaoRepositorio configuracao,
    IParametros parametros,
    IAutorizacao autorizacao,
    IUsuarioAtual usuarioAtual,
    IContextoEscolinha contexto,
    IRelogioBrasil relogio,
    OpcoesWhatsApp opcoes)
{
    public const int TamanhoPagina = 30;

    public bool PodeVer => autorizacao.Pode(Recurso.ConfiguracaoEscolinha, Operacao.Ler);

    public async Task<Pagina<MensagemNaLista>> ListarAsync(FiltroMensagens filtro, int pagina, CancellationToken ct = default)
    {
        pagina = Pagina<MensagemNaLista>.Normalizar(pagina);
        if (!PodeVer) return new Pagina<MensagemNaLista>([], 0, pagina, TamanhoPagina);
        var resultado = await repositorio.ListarMensagensAsync(filtro, pagina, TamanhoPagina, ct);
        return new Pagina<MensagemNaLista>(resultado.Itens.Select(m => new MensagemNaLista(m.Mensagem_ID, m.CriadaEm, m.Gatilho, m.Canal,
            m.DestinatarioNome, m.TipoDestinatario, m.Situacao, m.Motivo, m.AgendadaPara, m.RespondidaEm is not null, m.CreditosConsumidos)).ToList(),
            resultado.Total, resultado.Numero, resultado.Tamanho);
    }

    public async Task<DetalheMensagem?> DetalheAsync(Guid id, CancellationToken ct = default)
    {
        if (!PodeVer || await repositorio.DetalharMensagemAsync(id, ct) is not { } m) return null;
        return new DetalheMensagem(m.Mensagem_ID, m.Gatilho, m.Canal, m.DestinatarioNome, m.TipoDestinatario, m.Destinatario_ID, m.Telefone,
            m.Aluno_ID, m.Template, m.Conteudo, m.Situacao, m.Motivo, m.Essencial, m.Urgente, m.CriadaEm, m.AgendadaPara, m.EnviadaEm,
            m.EntregueEm, m.LidaEm, m.Tentativas, m.CreditosConsumidos,
            m.Eventos.OrderBy(e => e.Em).ThenBy(e => e.Tipo).Select(e => new EventoDaMensagem(e.Tipo, e.Em, e.Detalhe)).ToList(),
            m.Respostas.OrderBy(r => r.RecebidaEm).Select(r => new RespostaDaMensagem(r.RecebidaEm, r.Canal, r.Texto, r.Payload is not null,
                r.Tratada, r.Observacao)).ToList());
    }

    public async Task<ResumoMensageria?> ResumoAsync(int dias = 30, CancellationToken ct = default)
    {
        if (!PodeVer) return null;
        var desde = relogio.Agora().AddDays(-dias);
        var conta = await configuracao.ObterContaCreditosAsync(ct);
        var aviso = (await parametros.ObterAsync(ct)).Mensagem.AvisoSaldoCreditosAbaixoDe;
        return new ResumoMensageria(conta?.Saldo ?? 0, aviso, await repositorio.ContarRetidasAsync(ct),
            await repositorio.ContarPorSituacaoAsync(desde, ct), opcoes.Descricao, opcoes.Habilitado);
    }

    /// <summary>
    /// Os avisos do portal de quem está logado nesta escolinha. Abrir marca os não lidos como lidos (captura de leitura
    /// pelo portal, BL-M09-07).
    /// </summary>
    public async Task<IReadOnlyList<AvisoDoPortal>> AvisosDoPortalAsync(CancellationToken ct = default)
    {
        if (usuarioAtual.Id is not { } pessoaId || !contexto.Papeis.Contains(PapelAtuacao.Responsavel)) return [];
        var avisos = await repositorio.AvisosDoPortalAsync(pessoaId, 50, ct);
        var agora = relogio.Agora();
        var novos = avisos.Where(a => a.MarcarLidaNoPortal(agora)).Select(a => a.Mensagem_ID).ToHashSet();
        if (novos.Count > 0) await repositorio.SalvarAsync(ct);
        return avisos.Select(a => new AvisoDoPortal(a.Mensagem_ID, a.CriadaEm, a.Gatilho, a.Conteudo, novos.Contains(a.Mensagem_ID))).ToList();
    }

    public async Task<int> AvisosNaoLidosAsync(CancellationToken ct = default) =>
        usuarioAtual.Id is { } pessoaId && contexto.Papeis.Contains(PapelAtuacao.Responsavel)
            ? await repositorio.AvisosNaoLidosAsync(pessoaId, ct)
            : 0;
}

/// <summary>
/// A varredura do motor (a cada 5 minutos, por escolinha): libera as retidas quando o saldo e o canal voltam e reenfileira
/// o que ficou para trás (job perdido, fim da janela de silêncio). É o que garante que nada essencial se perde em silêncio.
/// </summary>
public class VarreduraMensagens(
    IMensageriaRepositorio repositorio,
    IConfiguracaoRepositorio configuracao,
    IAgendadorMensageria agendador,
    IContextoEscolinha contexto,
    OpcoesWhatsApp opcoes,
    IRelogioBrasil relogio)
{
    public const int Lote = 200;

    public async Task<(int Liberadas, int Enfileiradas)> VarrerAsync(CancellationToken ct = default)
    {
        var escolinhaId = contexto.EscolinhaId ?? throw new InvalidOperationException("Varredura sem escolinha no contexto.");
        var agora = relogio.Agora();
        var liberadas = 0;

        if (opcoes.Habilitado && (await configuracao.ObterContaCreditosAsync(ct))?.Saldo is > 0 and var saldo)
        {
            foreach (var id in await repositorio.RetidasAsync(Math.Min(saldo, Lote), ct))
            {
                if (await repositorio.ObterMensagemAsync(id, ct) is not { Situacao: SituacaoMensagem.Retida } retida) continue;
                retida.Liberar("A escolinha voltou a ter crédito e canal.", agora);
                liberadas++;
            }
            if (liberadas > 0) await repositorio.SalvarAsync(ct);
        }

        var prontas = await repositorio.ProntasParaEnviarAsync(agora, Lote, ct);
        foreach (var id in prontas) agendador.Enviar(escolinhaId, id);
        return (liberadas, prontas.Count);
    }
}
