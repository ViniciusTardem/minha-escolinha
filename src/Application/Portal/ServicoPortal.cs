using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Eventos;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Eventos;
using MinhaEscolinha.Domain.Financeiro;
using MinhaEscolinha.Domain.Presencas;

namespace MinhaEscolinha.Application.Portal;

/// <summary>
/// O portal do responsável (S6), no celular e sem instalar nada (R2): os filhos numa conta só (CA-15), os jogos com a
/// resposta (BL-M10-07), as mensalidades com o total do mês por filho e o pagamento pela Asaas (BL-M10-05, BL-M10-06, CA-16),
/// as presenças e os avisos. Tudo passa pelos filhos com vínculo confirmado (BL-M10-11): o aluno a confirmar não aparece,
/// nem o nome. Só para quem tem o papel Responsável; nada do portal é dirigido ao aluno (R4, BL-M10-12).
/// </summary>
public class ServicoPortal(
    IPortalRepositorio repositorio,
    IEventosRepositorio eventos,
    ITurmasRepositorio turmas,
    IConfiguracaoRepositorio configuracao,
    TextosEventos textos,
    ServicoMensagens mensagens,
    EmissorGatilhos emissor,
    IAutorizacao autorizacao,
    IUsuarioAtual usuarioAtual,
    IContextoEscolinha contexto,
    IParametros parametros,
    IRelogioBrasil relogio)
{
    public const int DiasFrequencia = 30;
    public const int DiasPresencas = 60;
    public const int DiasJogosEncerrados = 14;
    public const int MesesPagas = 3;

    private static readonly StringComparer OrdemDosNomes = StringComparer.Create(new System.Globalization.CultureInfo("pt-BR"), true);

    public bool EhResponsavel => usuarioAtual.Id is not null && contexto.Papeis.Contains(PapelAtuacao.Responsavel);

    public async Task<InicioPortal?> InicioAsync(CancellationToken ct = default)
    {
        if (await FamiliaAsync(ct) is not { } familia) return null;
        var agora = relogio.Agora();
        var alunos = familia.Filhos.Select(f => f.AlunoId).ToList();

        var convocacoes = alunos.Count == 0 ? [] : await repositorio.ConvocacoesAsync(alunos, agora, ct);
        var aResponder = convocacoes.Count(l => Responde(l.Convocacao, l.Evento, agora) && l.Convocacao.Situacao == SituacaoConvocacao.Enviada);
        var financeiros = familia.Filhos.Where(f => f.CuidaDasMensalidades).Select(f => f.AlunoId).ToList();
        var emAberto = financeiros.Count == 0
            ? []
            : (await repositorio.MensalidadesAsync(financeiros, DateOnly.MaxValue, ct)).Where(c => c.EmAberto && !c.ComSinal).ToList(); // só as em aberto

        var hoje = DateOnly.FromDateTime(agora);
        var presencas = alunos.Count == 0
            ? []
            : await repositorio.PresencasAsync(alunos, hoje.AddDays(-DiasFrequencia).ToDateTime(TimeOnly.MinValue), agora, ct);

        // BL-M10-02: a visão única de cada filho, com o próximo treino, o próximo jogo e o que está em aberto.
        var treinos = alunos.Count == 0 ? new Dictionary<Guid, DateTime>() : await repositorio.ProximosTreinosAsync(alunos, agora, ct);
        var resumos = familia.Filhos.ToDictionary(f => f.AlunoId, f =>
        {
            var jogo = convocacoes
                .Where(l => l.Convocacao.Aluno_ID == f.AlunoId && l.Evento.Aberto && l.Evento.Inicio > agora && l.Convocacao.Disparada
                            && l.Convocacao.Situacao != SituacaoConvocacao.Cancelada)
                .OrderBy(l => l.Evento.Inicio).Select(l => ((Convocacao, Evento)?)l).FirstOrDefault();
            var doFilho = f.CuidaDasMensalidades ? emAberto.Where(c => c.Aluno_ID == f.AlunoId).ToList() : [];
            return new ResumoDoFilho(treinos.TryGetValue(f.AlunoId, out var treino) ? treino : null,
                jogo is { } j ? (j.Item2.Adversario is { } adversario ? $"Jogo contra {adversario}" : "Jogo") : null,
                jogo?.Item2.Inicio, jogo?.Item1.Situacao,
                f.CuidaDasMensalidades ? doFilho.Sum(c => c.Valor) : null,
                doFilho.Count == 0 ? null : doFilho.Min(c => c.Vencimento));
        });

        return new InicioPortal(
            (await configuracao.ObterEscolinhaAsync(ct))?.Nome ?? "Escolinha",
            usuarioAtual.Nome ?? "Responsável",
            familia.Filhos,
            familia.AConfirmar,
            await mensagens.AvisosNaoLidosAsync(ct),
            aResponder,
            emAberto.Sum(c => c.Valor),
            emAberto.Count,
            familia.Filhos.ToDictionary(f => f.AlunoId, f => Frequencia(presencas.Where(p => p.AlunoId == f.AlunoId))),
            resumos);
    }

    public async Task<JogosPortal?> JogosAsync(CancellationToken ct = default)
    {
        if (await FamiliaAsync(ct) is not { } familia) return null;
        var agora = relogio.Agora();
        var nomes = familia.Filhos.ToDictionary(f => f.AlunoId, f => f.Nome);
        var linhas = nomes.Count == 0
            ? []
            : await repositorio.ConvocacoesAsync(nomes.Keys, agora.Date.AddDays(-DiasJogosEncerrados), ct);
        var locais = (await turmas.ListarLocaisAsync(ct)).ToDictionary(l => l.Local_ID);
        var minhasFichas = familia.Vinculos.Select(v => v.ResponsavelId).ToHashSet();

        var todas = new List<ConvocacaoNoPortal>();
        foreach (var (convocacao, evento) in linhas)
        {
            locais.TryGetValue(evento.Local_ID, out var local);
            var propria = convocacao.Origem is OrigemResposta.WhatsApp or OrigemResposta.Portal
                          && convocacao.RespondidoPor_ID is { } quem && minhasFichas.Contains(quem);
            todas.Add(new ConvocacaoNoPortal(convocacao.Convocacao_ID, convocacao.Aluno_ID, nomes[convocacao.Aluno_ID],
                evento.Titulo(await textos.TurmasAsync(evento, ct)), evento.Inicio, evento.Fim, local?.Nome ?? "—",
                local?.Endereco.LinkMapa(local.Nome), evento.Logistica, evento.Situacao, evento.Motivo, convocacao.Situacao,
                convocacao.Origem, convocacao.RespondidoPorNome, convocacao.RespondidaEm, convocacao.OutraResposta, propria,
                Responde(convocacao, evento, agora)));
        }

        return new JogosPortal(
            familia.Filhos,
            todas.Where(c => c.PodeResponder && c.Situacao == SituacaoConvocacao.Enviada).OrderBy(c => c.Inicio).ToList(),
            todas.Where(c => c.PodeResponder && c.Situacao != SituacaoConvocacao.Enviada).OrderBy(c => c.Inicio).ToList(),
            todas.Where(c => !c.PodeResponder).OrderByDescending(c => c.Inicio).ToList());
    }

    /// <summary>
    /// A resposta pelo portal (BL-M10-07), com a mesma regra do WhatsApp: vale a primeira (P03), quem respondeu pode mudar
    /// a própria resposta, e o que ainda estava na fila para a família sai (regra de ouro 5). O painel do jogo aberto é
    /// avisado depois da gravação.
    /// </summary>
    public async Task<ResultadoOperacao> ResponderAsync(Guid convocacaoId, bool vai, CancellationToken ct = default)
    {
        const string NaoEncontrada = "Convocação não encontrada.";
        if (await FamiliaAsync(ct) is not { } familia) return ResultadoOperacao.Erro(NaoEncontrada);
        if (await eventos.ObterConvocacaoAsync(convocacaoId, ct) is not { Disparada: true } convocacao
            || familia.Filhos.All(f => f.AlunoId != convocacao.Aluno_ID)
            || !await autorizacao.PodeAsync(Recurso.ResponderConvocacao, Operacao.Alterar, new AlvoEscopo(TipoAlvoEscopo.Aluno, convocacao.Aluno_ID), ct))
            return ResultadoOperacao.Erro(NaoEncontrada);

        var agora = relogio.Agora();
        if (await eventos.ObterAsync(convocacao.Evento_ID, ct) is not { } evento || !Responde(convocacao, evento, agora))
            return ResultadoOperacao.Erro("Este jogo já começou, foi cancelado ou adiado: a resposta não muda mais por aqui.");

        // A ficha de responsável da pessoa ligada a este filho: a mesma que recebe a convocação pelo WhatsApp.
        var vinculo = familia.Vinculos.First(v => v.AlunoId == convocacao.Aluno_ID && v.Verificacao == VerificacaoVinculo.Verificado);
        var resultado = convocacao.Responder(vai, OrigemResposta.Portal, vinculo.Responsavel, vinculo.ResponsavelId, agora, agora);
        if (resultado == ResultadoResposta.Aplicada)
            await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemConvocacao, convocacao.Convocacao_ID, "A família já respondeu pelo portal.", ct);
        await eventos.SalvarAsync(ct);

        var filho = EmissorGatilhos.PrimeiroNome(vinculo.Aluno);
        return resultado switch
        {
            ResultadoResposta.Aplicada => ResultadoOperacao.Ok($"{filho}: {Convocacao.RotuloResposta(vai).ToLowerInvariant()}. A escolinha já vê a resposta."),
            ResultadoResposta.Repetida => ResultadoOperacao.Ok($"{filho}: esta resposta já estava registrada."),
            ResultadoResposta.Posterior when convocacao.Origem == OrigemResposta.RegistroManual => ResultadoOperacao.Ok(
                $"A escolinha já tinha registrado a resposta de {filho}. A sua fica registrada para a escolinha conferir."),
            ResultadoResposta.Posterior => ResultadoOperacao.Ok(
                $"Outro responsável já tinha respondido por {filho}. A sua resposta fica registrada para a escolinha conferir."),
            _ => ResultadoOperacao.Erro("Esta convocação não recebe mais resposta.")
        };
    }

    public async Task<PagamentosPortal?> PagamentosAsync(CancellationToken ct = default)
    {
        if (await FamiliaAsync(ct) is not { } familia) return null;
        var agora = relogio.Agora();
        var hoje = DateOnly.FromDateTime(agora);
        var mes = new DateOnly(hoje.Year, hoje.Month, 1);
        var meus = familia.Filhos.Where(f => f.CuidaDasMensalidades).ToDictionary(f => f.AlunoId, f => f.Nome);
        var cobrancas = meus.Count == 0 ? [] : await repositorio.MensalidadesAsync(meus.Keys, mes.AddMonths(-MesesPagas), ct);
        var linhas = cobrancas.Select(c => Linha(c, meus[c.Aluno_ID], agora)).ToList();
        // Como o painel de pagamentos (D68): sem mensalidade no mês de hoje e com a do seguinte já projetada, vale o seguinte.
        if (!cobrancas.Any(c => c.Competencia == mes && !c.Encerrada) && cobrancas.Any(c => c.Competencia == mes.AddMonths(1) && !c.Encerrada))
            mes = mes.AddMonths(1);

        var porFilho = meus
            .Select(f =>
            {
                var doMes = cobrancas.Where(c => c.Aluno_ID == f.Key && c.Competencia == mes && !c.Encerrada).ToList();
                var pago = doMes.Where(c => c.Quitada).Sum(c => c.ValorPago ?? c.Valor);
                var aPagar = doMes.Where(c => !c.Quitada).Sum(c => c.Valor);
                return new MesDoFilho(f.Key, f.Value, pago + aPagar, pago, aPagar);
            })
            .OrderBy(f => f.Aluno)
            .ToList();

        return new PagamentosPortal(
            mes,
            familia.Filhos,
            porFilho,
            linhas.Where(l => l.Situacao is SituacaoCobranca.Emitida or SituacaoCobranca.Vencida).OrderBy(l => l.Vencimento).ThenBy(l => l.Aluno).ToList(),
            linhas.Where(l => l.Situacao == SituacaoCobranca.Prevista && l.Vencimento >= hoje).OrderBy(l => l.Vencimento).ThenBy(l => l.Aluno).ToList(),
            linhas.Where(l => l.Situacao is SituacaoCobranca.Paga or SituacaoCobranca.PagaEmAtraso)
                .OrderByDescending(l => l.PagaEm).ThenBy(l => l.Aluno).ToList(),
            familia.Filhos.Where(f => !f.CuidaDasMensalidades).Select(f => f.Nome).ToList());
    }

    /// <summary>As presenças de um filho confirmado; fora disso, <c>null</c> (a tela responde 404).</summary>
    public async Task<PresencasPortal?> PresencasAsync(Guid alunoId, CancellationToken ct = default)
    {
        if (await FamiliaAsync(ct) is not { } familia || familia.Filhos.FirstOrDefault(f => f.AlunoId == alunoId) is not { } filho
            || !await autorizacao.PodeAsync(Recurso.RegistroPresenca, Operacao.Ler, new AlvoEscopo(TipoAlvoEscopo.Aluno, alunoId), ct))
            return null;
        var agora = relogio.Agora();
        var ate = DateOnly.FromDateTime(agora);
        var de = ate.AddDays(-DiasPresencas);
        var presencas = await repositorio.PresencasAsync([alunoId], de.ToDateTime(TimeOnly.MinValue), agora, ct);
        return new PresencasPortal(filho, de, ate, Frequencia(presencas),
            presencas.Select(p => new PresencaNoPortal(p.Sessao, p.Turma, p.Situacao)).ToList());
    }

    private sealed record Familia(IReadOnlyList<VinculoNoPortal> Vinculos, IReadOnlyList<FilhoNoPortal> Filhos, int AConfirmar);

    /// <summary>
    /// Os filhos da pessoa: o aluno aparece quando algum vínculo dela com ele foi confirmado pela escolinha (o mesmo critério
    /// do escopo, D53). O que só tem vínculo a confirmar entra na contagem, sem nome.
    /// </summary>
    private async Task<Familia?> FamiliaAsync(CancellationToken ct)
    {
        if (!EhResponsavel) return null;
        var vinculos = await repositorio.VinculosAsync(usuarioAtual.Id!.Value, ct);
        var confirmados = vinculos.Where(v => v.Verificacao == VerificacaoVinculo.Verificado).GroupBy(v => v.AlunoId).ToList();
        var turmasPorAluno = confirmados.Count == 0
            ? new Dictionary<Guid, IReadOnlyList<string>>()
            : await repositorio.TurmasAtivasAsync(confirmados.Select(g => g.Key).ToList(), ct);
        // P50: a escolinha pode abrir as mensalidades a todo responsável com vínculo confirmado.
        var todosVeem = (await parametros.ObterAsync(ct)).Financeiro.TodosResponsaveisVeemNoPortal;
        var filhos = confirmados
            .Select(g => new FilhoNoPortal(g.Key, g.First().Aluno, g.First().Situacao, turmasPorAluno.GetValueOrDefault(g.Key, []),
                todosVeem || g.Any(v => v.CuidaDasMensalidades)))
            .OrderBy(f => f.Nome, OrdemDosNomes)
            .ToList();
        var aConfirmar = vinculos.GroupBy(v => v.AlunoId)
            .Count(g => g.Any(v => v.Verificacao == VerificacaoVinculo.Pendente) && g.All(v => v.Verificacao != VerificacaoVinculo.Verificado));
        return new Familia(vinculos, filhos, aConfirmar);
    }

    /// <summary>A família ainda pode responder: o jogo está de pé, não começou, e a convocação saiu e não foi cancelada.</summary>
    private static bool Responde(Convocacao c, Evento e, DateTime agora) =>
        e.Aberto && e.Inicio > agora && c.Situacao is SituacaoConvocacao.Enviada or SituacaoConvocacao.Confirmada or SituacaoConvocacao.Recusada;

    private static FrequenciaNoPortal Frequencia(IEnumerable<PresencaDoFilho> presencas)
    {
        var lista = presencas.ToList();
        return new FrequenciaNoPortal(lista.Count(p => p.Situacao == SituacaoPresenca.Presente), lista.Count(p => p.Situacao == SituacaoPresenca.Falta),
            lista.Count(p => p.Situacao == SituacaoPresenca.Justificada));
    }

    private static MensalidadeNoPortal Linha(Cobranca c, string aluno, DateTime agora) =>
        new(c.Cobranca_ID, c.Aluno_ID, aluno, c.Competencia, c.Vencimento, c.Valor, c.Situacao, c.ComSinal, c.PagaEm, c.ValorPago,
            c.EmAberto && !c.ComSinal ? c.UrlFatura : null, c.PixValidoEm(agora) ? c.PixCopiaECola : null,
            c.PixValidoEm(agora) ? c.PixImagem : null);
}
