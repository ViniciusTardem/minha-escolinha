using System.Globalization;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Application.Presencas;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Eventos;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Application.Eventos;

/// <summary>
/// A convocação (BL-M07-05 a BL-M07-09): a montagem do fim de semana numa tela só — os jogos da semana, a escolha dos
/// convocados de cada um, com os sinais, e o disparo de tudo de uma vez (BL-M07-08, CA-10) —, o painel de confirmações
/// e o registro da resposta que veio por fora. As mensagens saem pelo motor único (R3), aos responsáveis (R4).
/// </summary>
public class ServicoConvocacoes(
    IEventosRepositorio eventos,
    ServicoEventos servicoEventos,
    TextosEventos textos,
    ITurmasRepositorio turmas,
    IEquipeRepositorio equipe,
    IConfiguracaoRepositorio configuracao,
    IAutorizacao autorizacao,
    IEscopoDeLeitura escopo,
    IParametros parametros,
    IRiscoEvasao risco,
    IUsuarioAtual usuario,
    EmissorGatilhos emissor,
    IMensageriaRepositorio mensageria,
    IRelogioBrasil relogio)
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <summary>Segunda-feira da semana do dia.</summary>
    public static DateOnly InicioDaSemana(DateOnly dia) => dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));

    /// <summary>A semana (segunda a domingo) com os eventos no escopo de quem vê, a seleção e os sinais (BL-M07-05).</summary>
    public async Task<SemanaConvocacoes?> SemanaAsync(DateOnly? dia, CancellationToken ct = default)
    {
        if (!servicoEventos.PodeVer) return null;
        var inicio = InicioDaSemana(dia ?? DateOnly.FromDateTime(relogio.Hoje()));
        var fim = inicio.AddDays(6);
        var alcance = await escopo.TurmasAsync(Recurso.EventosConvocacao, ct);
        var lista = await eventos.ListarAsync(inicio.ToDateTime(TimeOnly.MinValue), fim.AddDays(1).ToDateTime(TimeOnly.MinValue), alcance, ct);
        return new SemanaConvocacoes(inicio, fim, await MontarAsync(lista, alcance, ct), await servicoEventos.FormularioAsync(ct),
            servicoEventos.PodeMontar);
    }

    /// <summary>O painel de confirmações (BL-M07-09); fora do escopo, <c>null</c>.</summary>
    public async Task<PainelEvento?> PainelAsync(Guid id, CancellationToken ct = default)
    {
        if (await servicoEventos.ObterNoEscopoAsync(id, Operacao.Ler, ct) is not { } evento) return null;
        var alvo = new AlvoEscopo(TipoAlvoEscopo.Evento, id);
        var podeRegistrar = !evento.Encerrado && await autorizacao.PodeAsync(Recurso.ResponderConvocacao, Operacao.Alterar, alvo, ct);
        var podeAlterar = evento.Aberto && await autorizacao.PodeAsync(Recurso.EventosConvocacao, Operacao.Alterar, alvo, ct);

        var p = (await parametros.ObterAsync(ct)).Convocacao;
        var todas = await eventos.ConvocacoesComHistoricoAsync([id], ct);
        // P47: no jogo de várias turmas, o professor vê e registra só os convocados das turmas dele (a gestão vê todos).
        var alcance = await escopo.TurmasAsync(Recurso.EventosConvocacao, ct);
        var soAsDele = p.ProfessorSoPropriaTurma && !alcance.Todas;
        var convocacoes = soAsDele ? todas.Where(c => alcance.Inclui(c.Turma_ID)).ToList() : todas;
        var alunos = await eventos.NomesDosAlunosAsync(convocacoes.Select(c => c.Aluno_ID).Distinct().ToList(), ct);
        var nomesTurmas = await eventos.NomesDasTurmasAsync(evento.IdsDasTurmas, ct);
        var local = await turmas.ObterLocalAsync(evento.Local_ID, ct);
        var professor = await equipe.ObterProfessorAsync(evento.Professor_ID, ct);
        var turmasTexto = await textos.TurmasAsync(evento, ct);
        // P48: o que a família escreveu em texto, na linha do convocado, para quem convoca registrar por fora.
        var escritos = await mensageria.UltimosTextosDasOrigensAsync(EmissorGatilhos.OrigemConvocacao,
            convocacoes.Select(c => c.Convocacao_ID).ToList(), ct);
        // P51: a resposta que mudou perto do jogo fica em destaque.
        var janela = evento.Inicio.AddHours(-p.AvisoMudancaRespostaHorasAntes);

        var linhas = convocacoes
            .Select(c => new LinhaConvocado(c.Convocacao_ID, c.Aluno_ID, alunos.GetValueOrDefault(c.Aluno_ID, "—"),
                nomesTurmas.GetValueOrDefault(c.Turma_ID, "—"), c.Situacao, c.Origem, c.RespondidoPorNome, c.RespondidaEm, c.LembreteEm,
                c.OutraResposta, podeRegistrar && c.PodeRegistrarPorFora, c.MudouRespostaEntre(janela, evento.Inicio),
                escritos.TryGetValue(c.Convocacao_ID, out var escrito) ? $"“{escrito.Texto}” · {escrito.Em:dd/MM HH:mm}" : null))
            .OrderBy(l => Ordem(l.Situacao)).ThenBy(l => l.Aluno, StringComparer.Create(PtBr, true))
            .ToList();

        var modalidade = (await configuracao.ListarModalidadesAsync(ct)).FirstOrDefault(m => m.Modalidade_ID == evento.Modalidade_ID);
        var podePlacar = modalidade is not null && PoliticaPerfil.Recursos(evento.Tipo, modalidade.Recursos).PermitePlacar
                         && evento.Situacao is SituacaoEvento.Realizado or SituacaoEvento.ResultadoHomologado
                         && await autorizacao.PodeAsync(Recurso.EventosConvocacao, Operacao.Alterar, alvo, ct);

        return new PainelEvento(evento.Evento_ID, evento.Titulo(turmasTexto), turmasTexto, evento.Inicio, evento.Fim, local?.Nome ?? "—",
            local is null ? null : NuloSeVazio(local.Endereco.Resumo()), local?.Endereco.LinkMapa(local.Nome), evento.Adversario,
            evento.Logistica, professor?.Nome ?? "—", evento.Situacao, evento.Motivo,
            ContagemConvocacoes.De(convocacoes.Select(c => c.Situacao)), await MinimoAsync(evento, ct), linhas, podeAlterar,
            relogio.Agora(), evento.PlacarNos, evento.PlacarAdversario, podePlacar, soAsDele && convocacoes.Count < todas.Count);
    }

    /// <summary>
    /// As respostas que mudaram perto dos jogos que ainda vão acontecer (P51), no escopo de quem vê: para o painel inicial
    /// do professor e da gestão.
    /// </summary>
    public async Task<IReadOnlyList<MudancaPertoDoJogo>> MudancasPertoDoJogoAsync(CancellationToken ct = default)
    {
        if (!servicoEventos.PodeVer) return [];
        var agora = relogio.Agora();
        var p = (await parametros.ObterAsync(ct)).Convocacao;
        var alcance = await escopo.TurmasAsync(Recurso.EventosConvocacao, ct);
        var lista = (await eventos.ListarAsync(agora, agora.AddHours(p.AvisoMudancaRespostaHorasAntes), alcance, ct)).Where(e => e.Aberto).ToList();
        if (lista.Count == 0) return [];
        var convocacoes = (await eventos.ConvocacoesComHistoricoAsync(lista.Select(e => e.Evento_ID).ToList(), ct))
            .Where(c => alcance.Todas || !p.ProfessorSoPropriaTurma || alcance.Inclui(c.Turma_ID)).ToLookup(c => c.Evento_ID);
        var resultado = new List<MudancaPertoDoJogo>();
        foreach (var evento in lista)
        {
            var mudaram = convocacoes[evento.Evento_ID]
                .Where(c => c.MudouRespostaEntre(evento.Inicio.AddHours(-p.AvisoMudancaRespostaHorasAntes), evento.Inicio)).ToList();
            if (mudaram.Count == 0) continue;
            var nomes = await eventos.NomesDosAlunosAsync(mudaram.Select(c => c.Aluno_ID).ToList(), ct);
            var titulo = evento.Titulo(await textos.TurmasAsync(evento, ct));
            resultado.AddRange(mudaram.Select(c => new MudancaPertoDoJogo(evento.Evento_ID, titulo, evento.Inicio, nomes.GetValueOrDefault(c.Aluno_ID, "—"))));
        }
        return resultado;
    }

    /// <summary>
    /// Grava a escolha dos convocados dos eventos da tela (rascunho da montagem): marca quem entrou e tira quem saiu, antes
    /// do disparo. A convocação que já saiu não muda por aqui.
    /// </summary>
    /// <param name="eventosDaTela">Os eventos que estavam na tela (a caixa desmarcada não vem no formulário).</param>
    public async Task<ResultadoOperacao> SalvarSelecaoAsync(IReadOnlyCollection<Guid> eventosDaTela, IReadOnlyCollection<(Guid Evento, Guid Aluno)> marcados,
        CancellationToken ct = default)
    {
        if (!servicoEventos.PodeMontar) return ResultadoOperacao.Erro("Só a gestão e os professores convocam.");
        var agora = relogio.Agora();
        var hoje = DateOnly.FromDateTime(agora);
        var autor = usuario.Nome ?? "—";
        var p = (await parametros.ObterAsync(ct)).Convocacao;
        var alcance = await escopo.TurmasAsync(Recurso.EventosConvocacao, ct);
        var modalidades = (await configuracao.ListarModalidadesAsync(ct)).ToDictionary(m => m.Modalidade_ID);

        int entraram = 0, sairam = 0;
        var avisos = new List<string>();
        foreach (var id in eventosDaTela.Distinct())
        {
            if (await servicoEventos.ObterNoEscopoAsync(id, Operacao.Alterar, ct) is not { } evento
                || MotivoSemConvocacao(evento, modalidades, agora) is not null) continue;

            var elegiveis = (await eventos.AlunosConvocaveisAsync(evento.IdsDasTurmas.Where(alcance.Inclui).ToList(), DateOnly.FromDateTime(evento.Inicio), ct))
                .GroupBy(a => a.AlunoId).ToDictionary(g => g.Key, g => g.First());
            var existentes = (await eventos.ConvocacoesAsync([id], ct)).ToDictionary(c => c.Aluno_ID);
            var desejados = marcados.Where(m => m.Evento == id).Select(m => m.Aluno).ToHashSet();
            var bloqueados = p.BloquearInadimplente
                ? await eventos.InadimplentesAsync(desejados.ToList(), hoje.AddDays(-p.DiasAtrasoSinalizacao), ct)
                : new HashSet<Guid>();

            foreach (var alunoId in desejados)
            {
                if (existentes.ContainsKey(alunoId) || !elegiveis.TryGetValue(alunoId, out var aluno)) continue;
                if (bloqueados.Contains(alunoId))
                {
                    avisos.Add($"{aluno.Nome} está com mensalidade em atraso, e a escolinha bloqueia a convocação de inadimplente.");
                    continue;
                }
                eventos.AdicionarConvocacao(Convocacao.Selecionar(id, alunoId, aluno.TurmaId, agora, autor));
                entraram++;
            }
            foreach (var convocacao in existentes.Values.Where(c => c.Situacao == SituacaoConvocacao.Selecionada && !desejados.Contains(c.Aluno_ID)))
            {
                eventos.RemoverConvocacao(convocacao);
                sairam++;
            }
        }

        await eventos.SalvarAsync(ct);
        var mensagem = entraram + sairam == 0 ? "A escolha dos convocados já estava salva." : "Escolha dos convocados salva.";
        return ResultadoOperacao.Ok(string.Join(" ", new[] { mensagem }.Concat(avisos)));
    }

    /// <summary>
    /// O disparo (BL-M07-07, BL-M07-08, CA-10): as convocações escolhidas de todos os eventos da tela saem num fluxo só, numa
    /// gravação só. Cada convocado gera uma mensagem por responsável que recebe notificação (CA-11), com o nome dele (CA-13).
    /// Na hora, ou no começo da próxima janela de disparo, quando a escolinha pede (RF-CON-003).
    /// </summary>
    public async Task<ResultadoOperacao> DispararAsync(IReadOnlyCollection<Guid> eventosDaTela, CancellationToken ct = default)
    {
        if (!servicoEventos.PodeMontar) return ResultadoOperacao.Erro("Só a gestão e os professores convocam.");
        var agora = relogio.Agora();
        var valores = await parametros.ObterAsync(ct);
        var p = valores.Convocacao;
        var agendarPara = p.SoDentroDaJanela ? PoliticaEnvio.ProximoInicioDaJanela(p.JanelaDisparo, agora) : null;
        // Quando as mensagens saem de fato: no começo da janela de disparo, se a escolinha pede, e depois da janela de
        // silêncio, que vale por cima (a convocação não é urgente). Quem dispara fica sabendo.
        var saida = agendarPara ?? agora;
        var saidaReal = PoliticaEnvio.FimDaJanelaDeSilencio(valores.Mensagem.JanelaSilencio, saida) ?? saida;
        var modalidades = (await configuracao.ListarModalidadesAsync(ct)).ToDictionary(m => m.Modalidade_ID);

        int convocados = 0, jogos = 0, semNinguem = 0;
        foreach (var id in eventosDaTela.Distinct())
        {
            if (await servicoEventos.ObterNoEscopoAsync(id, Operacao.Alterar, ct) is not { } evento
                || MotivoSemConvocacao(evento, modalidades, agora) is not null) continue;
            var escolhidas = (await eventos.ConvocacoesAsync([id], ct)).Where(c => c.Situacao == SituacaoConvocacao.Selecionada).ToList();
            if (escolhidas.Count == 0) continue;

            var dados = await textos.ParaMensagemAsync(evento, ct);
            var nomes = await eventos.NomesDosAlunosAsync(escolhidas.Select(c => c.Aluno_ID).ToList(), ct);
            foreach (var convocacao in escolhidas)
            {
                convocacao.Disparar(agora);
                if (await emissor.ConvocacaoAsync(convocacao, dados, nomes.GetValueOrDefault(convocacao.Aluno_ID, "—"), agendarPara, ct) == 0)
                    semNinguem++;
                convocados++;
            }
            jogos++;
        }
        if (convocados == 0) return ResultadoOperacao.Erro("Nenhuma convocação escolhida para disparar.");

        await eventos.SalvarAsync(ct);
        var texto = $"{convocados} convocação(ões) disparada(s) em {jogos} jogo(s).";
        if (saidaReal > agora)
            texto += saidaReal == agendarPara
                ? $" Fora da janela de disparo: as mensagens saem às {saidaReal:HH:mm} de {saidaReal:dd/MM}."
                : $" Na janela de silêncio: as mensagens saem às {saidaReal:HH:mm} de {saidaReal:dd/MM}.";
        if (semNinguem > 0) texto += $" {semNinguem} convocado(s) sem responsável que receba mensagem: registre a resposta por fora.";
        return ResultadoOperacao.Ok(texto);
    }

    /// <summary>A resposta que veio por fora (spec §6, §7): o professor ou o gestor sobrescreve o estado, com autoria.</summary>
    public async Task<ResultadoOperacao> RegistrarPorForaAsync(Guid convocacaoId, bool vai, CancellationToken ct = default)
    {
        if (await eventos.ObterConvocacaoAsync(convocacaoId, ct) is not { } convocacao) return ResultadoOperacao.Erro("Convocação não encontrada.");
        if (!await autorizacao.PodeAsync(Recurso.ResponderConvocacao, Operacao.Alterar, new AlvoEscopo(TipoAlvoEscopo.Evento, convocacao.Evento_ID), ct))
            return ResultadoOperacao.Erro("Só a gestão e o professor do jogo registram a resposta.");
        if (await eventos.ObterAsync(convocacao.Evento_ID, ct) is not { Encerrado: false })
            return ResultadoOperacao.Erro("O jogo foi cancelado ou adiado.");
        // P47: o professor registra só os convocados das turmas dele.
        var alcance = await escopo.TurmasAsync(Recurso.EventosConvocacao, ct);
        if ((await parametros.ObterAsync(ct)).Convocacao.ProfessorSoPropriaTurma && !alcance.Inclui(convocacao.Turma_ID))
            return ResultadoOperacao.Erro("Este convocado é de uma turma de outro professor: o registro fica com ele ou com a gestão.");
        if (!convocacao.PodeRegistrarPorFora)
            return ResultadoOperacao.Erro($"Convocação {Convocacao.Rotulo(convocacao.Situacao).ToLowerInvariant()} não recebe resposta.");

        convocacao.RegistrarPorFora(vai, usuario.Nome ?? "—", usuario.Id, relogio.Agora());
        await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemConvocacao, convocacao.Convocacao_ID, "A resposta foi registrada por fora.", ct);
        await eventos.SalvarAsync(ct);
        var nome = (await eventos.NomesDosAlunosAsync([convocacao.Aluno_ID], ct)).GetValueOrDefault(convocacao.Aluno_ID, "Convocado");
        return ResultadoOperacao.Ok($"{nome}: {Convocacao.RotuloResposta(vai).ToLowerInvariant()} (registrado por fora).");
    }

    /// <summary>
    /// Por que o evento não aceita convocação agora (<c>null</c> = aceita): encerrado ou realizado, já começou, ou a
    /// modalidade não tem o recurso "convocação" (R8).
    /// </summary>
    public static string? MotivoSemConvocacao(Evento evento, IReadOnlyDictionary<Guid, Modalidade> modalidades, DateTime agora)
    {
        if (!evento.Aberto) return $"Jogo {Evento.Rotulo(evento.Situacao).ToLowerInvariant()}.";
        if (evento.Inicio <= agora) return "O jogo já começou.";
        if (!modalidades.TryGetValue(evento.Modalidade_ID, out var modalidade)
            || !PoliticaPerfil.Recursos(evento.Tipo, modalidade.Recursos).PermiteConvocacao)
            return "A modalidade deste jogo não tem convocação.";
        return null;
    }

    private async Task<IReadOnlyList<EventoNaSemana>> MontarAsync(IReadOnlyList<Evento> lista, EscopoTurmas alcance, CancellationToken ct)
    {
        if (lista.Count == 0) return [];
        var agora = relogio.Agora();
        var hoje = DateOnly.FromDateTime(agora);
        var p = (await parametros.ObterAsync(ct)).Convocacao;
        var modalidades = (await configuracao.ListarModalidadesAsync(ct)).ToDictionary(m => m.Modalidade_ID);
        var locais = (await turmas.ListarLocaisAsync(ct)).ToDictionary(l => l.Local_ID, l => l.Nome);
        var nomesTurmas = await eventos.NomesDasTurmasAsync(lista.SelectMany(e => e.IdsDasTurmas).Distinct().ToList(), ct);
        var convocacoes = (await eventos.ConvocacoesAsync(lista.Select(e => e.Evento_ID).ToList(), ct)).ToLookup(c => c.Evento_ID);
        var emRisco = (await risco.AlunosEmRiscoAsync(ct)).Select(r => r.AlunoId).ToHashSet();
        var podeMontar = servicoEventos.PodeMontar;

        var resultado = new List<EventoNaSemana>();
        foreach (var evento in lista.OrderBy(e => e.Inicio))
        {
            var doEvento = convocacoes[evento.Evento_ID].ToDictionary(c => c.Aluno_ID);
            var elegiveis = (await eventos.AlunosConvocaveisAsync(evento.IdsDasTurmas.Where(alcance.Inclui).ToList(), DateOnly.FromDateTime(evento.Inicio), ct))
                .GroupBy(a => a.AlunoId).Select(g => g.First()).ToList();
            var ids = elegiveis.Select(a => a.AlunoId).Concat(doEvento.Keys).Distinct().ToList();
            var nomes = await eventos.NomesDosAlunosAsync(ids, ct);
            var inadimplentes = p.SinalizarInadimplencia || p.BloquearInadimplente
                ? await eventos.InadimplentesAsync(ids, hoje.AddDays(-p.DiasAtrasoSinalizacao), ct)
                : new HashSet<Guid>();
            var outros = (await eventos.ConvocacoesAtivasAsync(ids, evento.Inicio.Date, evento.Inicio.Date.AddDays(1), ct)).ToLookup(c => c.AlunoId);

            var alunos = ids.Select(id =>
                {
                    var elegivel = elegiveis.FirstOrDefault(a => a.AlunoId == id);
                    var convocacao = doEvento.GetValueOrDefault(id);
                    var turmaId = convocacao?.Turma_ID ?? elegivel!.TurmaId;
                    var conflitos = RegrasConvocacao.Conflitos(evento.Evento_ID, evento.Inicio, evento.Fim, outros[id])
                        .Select(c => $"Também no {TextosEventos.Minusculo(c.Evento)}, {c.Inicio:HH:mm}–{c.Fim:HH:mm}").ToList();
                    var inadimplente = inadimplentes.Contains(id);
                    return new AlunoNaSelecao(id, nomes.GetValueOrDefault(id, "—"), turmaId, nomesTurmas.GetValueOrDefault(turmaId, "—"),
                        convocacao?.Situacao, elegivel?.Trial ?? false, emRisco.Contains(id), inadimplente && p.SinalizarInadimplencia,
                        inadimplente && p.BloquearInadimplente, elegivel is { TemCelular: false }, conflitos);
                })
                .OrderBy(a => a.Turma, StringComparer.Create(PtBr, true)).ThenBy(a => a.Nome, StringComparer.Create(PtBr, true))
                .ToList();

            var motivo = podeMontar ? MotivoSemConvocacao(evento, modalidades, agora) : "Só consulta.";
            var alertas = evento.Aberto && evento.Inicio > agora
                ? (await servicoEventos.AlertasAsync(evento.Evento_ID, evento.Inicio, evento.Fim, evento.Professor_ID, evento.Local_ID, ct))
                    .Select(a => a.Descricao).ToList()
                : [];
            var turmasTexto = EmissorGatilhos.Juntar(evento.IdsDasTurmas.Select(t => nomesTurmas.GetValueOrDefault(t, "—")).OrderBy(n => n));
            resultado.Add(new EventoNaSemana(evento.Evento_ID, evento.Titulo(turmasTexto), evento.Inicio, evento.Fim,
                locais.GetValueOrDefault(evento.Local_ID, "—"), evento.Situacao, ContagemConvocacoes.De(doEvento.Values.Select(c => c.Situacao)),
                await MinimoAsync(evento, ct), motivo is null, motivo, alertas, alunos));
        }
        return resultado;
    }

    /// <summary>O mínimo de confirmados do jogo: o maior entre as turmas dele (parâmetro de turma, BL-M02-12).</summary>
    private async Task<int> MinimoAsync(Evento evento, CancellationToken ct)
    {
        var minimos = new List<int>();
        foreach (var id in evento.IdsDasTurmas)
            if (await turmas.ObterTurmaAsync(id, ct) is { } turma) minimos.Add(turma.MinimoConfirmados);
        return minimos.Count == 0 ? 0 : minimos.Max();
    }

    private static int Ordem(SituacaoConvocacao situacao) => situacao switch
    {
        SituacaoConvocacao.Enviada => 0,
        SituacaoConvocacao.Confirmada => 1,
        SituacaoConvocacao.Recusada => 2,
        SituacaoConvocacao.SemResposta => 3,
        SituacaoConvocacao.Selecionada => 4,
        _ => 5
    };

    private static string? NuloSeVazio(string texto) => string.IsNullOrWhiteSpace(texto) ? null : texto;
}
