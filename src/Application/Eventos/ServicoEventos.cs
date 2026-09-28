using System.Globalization;
using FluentValidation;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Agenda;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Eventos;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Application.Eventos;

/// <summary>Os textos do evento para as telas e as mensagens: o título, o quando, o local e o link de mapa.</summary>
public class TextosEventos(IEventosRepositorio eventos, ITurmasRepositorio turmas)
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <summary>"sábado, 03/10 às 09:00".</summary>
    public static string Quando(DateTime inicio) => inicio.ToString("dddd, dd/MM 'às' HH:mm", PtBr);

    public static string Minusculo(string texto) => texto.Length == 0 ? texto : char.ToLower(texto[0], PtBr) + texto[1..];

    /// <summary>"Sub-11 e Sub-13".</summary>
    public async Task<string> TurmasAsync(Evento evento, CancellationToken ct = default)
    {
        var nomes = await eventos.NomesDasTurmasAsync(evento.IdsDasTurmas, ct);
        return EmissorGatilhos.Juntar(evento.IdsDasTurmas.Select(id => nomes.GetValueOrDefault(id, "—"))
            .OrderBy(n => n, StringComparer.Create(PtBr, true)));
    }

    public async Task<DadosEventoMensagem> ParaMensagemAsync(Evento evento, CancellationToken ct = default)
    {
        var titulo = evento.Titulo(await TurmasAsync(evento, ct));
        var local = await turmas.ObterLocalAsync(evento.Local_ID, ct);
        return new DadosEventoMensagem(Minusculo(titulo), Quando(evento.Inicio), local?.Nome ?? "—",
            local?.Endereco.LinkMapa(local.Nome) ?? "—", evento.Inicio);
    }
}

/// <summary>
/// O evento (BL-M07-01, BL-M07-03, BL-M07-13): criar, alterar, confirmar, realizar, cancelar e adiar. Quem cria é a gestão
/// (qualquer turma) ou o professor (as turmas dele, BL-M07-18). O tipo e o adversário seguem o perfil da modalidade (R8).
/// Os alertas de agenda do professor e do local pedem confirmação e ficam registrados (R9: sinaliza, não bloqueia).
/// Alterar o quando ou o onde avisa as famílias dos convocados; cancelar e adiar avisam e cancelam as convocações (CA-14).
/// </summary>
public class ServicoEventos(
    IEventosRepositorio eventos,
    ITurmasRepositorio turmas,
    IEquipeRepositorio equipe,
    IConfiguracaoRepositorio configuracao,
    IOcupacaoAgenda agenda,
    IAutorizacao autorizacao,
    IEscopoDeLeitura escopo,
    IUsuarioAtual usuario,
    EmissorGatilhos emissor,
    TextosEventos textos,
    RegistroDeAlteracoes registro,
    IParametros parametros,
    IRelogioBrasil relogio,
    IValidator<EventoRequisicao> validador,
    IValidator<EncerrarEventoRequisicao> validadorEncerrar)
{
    public const string Entidade = nameof(Evento);

    public bool PodeVer => autorizacao.Pode(Recurso.EventosConvocacao, Operacao.Ler);
    public bool PodeMontar => autorizacao.Pode(Recurso.EventosConvocacao, Operacao.Alterar);
    private bool Gestao => autorizacao.Nivel(Recurso.EventosConvocacao) == NivelAcesso.Total;

    /// <summary>O evento no escopo de quem pede (fora dele, <c>null</c>: a tela responde 404).</summary>
    public async Task<Evento?> ObterNoEscopoAsync(Guid id, Operacao operacao, CancellationToken ct = default)
    {
        if (await eventos.ObterAsync(id, ct) is not { } evento) return null;
        return await autorizacao.PodeAsync(Recurso.EventosConvocacao, operacao, new AlvoEscopo(TipoAlvoEscopo.Evento, id), ct) ? evento : null;
    }

    /// <summary>As opções do formulário no escopo de quem monta: as turmas ativas (de modalidade que tem o tipo jogo), os locais e os professores.</summary>
    public async Task<FormularioEvento> FormularioAsync(CancellationToken ct = default)
    {
        var alcance = await escopo.TurmasAsync(Recurso.EventosConvocacao, ct);
        var modalidades = (await configuracao.ListarModalidadesAsync(ct)).ToDictionary(m => m.Modalidade_ID);
        var lista = (await turmas.ListarTurmasAsync(alcance, incluirEncerradas: false, ct))
            .Where(t => modalidades.TryGetValue(t.Modalidade_ID, out var m) && PoliticaPerfil.TipoSuportado(m.Perfil, TipoEvento.Jogo))
            .OrderBy(t => t.Nome)
            .Select(t => new OpcaoTurmaEvento(t.Turma_ID, t.Nome, t.Modalidade_ID, t.Professor_ID, t.Local_ID, t.MinimoConfirmados))
            .ToList();
        var locais = (await turmas.ListarLocaisAsync(ct)).OrderBy(l => l.Nome).Select(l => new OpcaoEvento(l.Local_ID, l.Nome)).ToList();
        var professores = Gestao
            ? (await equipe.ListarProfessoresAsync(ct)).OrderBy(p => p.Nome).Select(p => new OpcaoEvento(p.Professor_ID, p.Nome)).ToList()
            : [];
        var duracao = lista.Count > 0 && modalidades.TryGetValue(lista[0].ModalidadeId, out var primeira)
            ? primeira.DuracaoPadraoEventoMinutos
            : Modalidade.DuracaoPadraoEventoProvisoria;
        return new FormularioEvento(lista, locais, professores, duracao, Gestao);
    }

    public async Task<ResultadoOperacao> CriarAsync(EventoRequisicao r, CancellationToken ct = default)
    {
        if (!PodeMontar) return ResultadoOperacao.Erro("Só a gestão e os professores montam jogos.");
        var validacao = await validador.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);

        var formulario = await FormularioAsync(ct);
        var escolhidas = formulario.Turmas.Where(t => r.Turmas.Contains(t.Id)).ToList();
        if (escolhidas.Count != r.Turmas.Distinct().Count())
            return ResultadoOperacao.Erro("Escolha turmas ativas que você acompanha.", nameof(r.Turmas));
        if (escolhidas.Select(t => t.ModalidadeId).Distinct().Count() > 1)
            return ResultadoOperacao.Erro("As turmas de um jogo são da mesma modalidade.", nameof(r.Turmas));

        var modalidade = (await configuracao.ListarModalidadesAsync(ct)).First(m => m.Modalidade_ID == escolhidas[0].ModalidadeId);
        r.LocalId ??= escolhidas[0].LocalId; // o jogo herda o local da turma, como o professor
        if (await ConferirAsync(r, modalidade, ct) is { } erro) return erro;
        var professorId = await ProfessorAsync(r, escolhidas[0].ProfessorId, ct);
        if (professorId is null) return ResultadoOperacao.Erro("Escolha o professor responsável.", nameof(r.ProfessorId));

        var inicio = r.Inicio!.Value;
        var fim = inicio.AddMinutes(r.DuracaoMinutos!.Value);
        var alertas = await AlertasAsync(null, inicio, fim, professorId.Value, r.LocalId!.Value, ct);
        if (alertas.Count > 0 && !r.ConfirmarAlertas) return PedirConfirmacao(alertas);

        var recursos = PoliticaPerfil.Recursos(TipoEvento.Jogo, modalidade.Recursos);
        var evento = Evento.Novo(TipoEvento.Jogo, modalidade.Modalidade_ID, escolhidas.Select(t => t.Id).ToList(), inicio, r.DuracaoMinutos.Value,
            r.LocalId.Value, professorId.Value, r.Adversario, recursos.PermiteAdversario, r.Logistica);
        eventos.Adicionar(evento);
        if (alertas.Count > 0) RegistrarAlertas(evento.Evento_ID, alertas);
        await eventos.SalvarAsync(ct);
        var titulo = evento.Titulo(await textos.TurmasAsync(evento, ct));
        return ResultadoOperacao.Ok($"{titulo}: {TextosEventos.Quando(inicio)}. Agora escolha os convocados.", evento.Evento_ID);
    }

    /// <summary>Muda o evento aberto. Se mudou o quando ou o onde, as famílias dos convocados são avisadas (uma vez por família).</summary>
    public async Task<ResultadoOperacao> AlterarAsync(EventoRequisicao r, CancellationToken ct = default)
    {
        if (r.Id is not { } id || await ObterNoEscopoAsync(id, Operacao.Alterar, ct) is not { } evento)
            return ResultadoOperacao.Erro("Jogo não encontrado.");
        if (!evento.Aberto) return ResultadoOperacao.Erro($"Jogo {Evento.Rotulo(evento.Situacao).ToLowerInvariant()} não muda mais.");
        r.Turmas = [.. evento.IdsDasTurmas];
        var validacao = await validador.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);

        var modalidade = (await configuracao.ListarModalidadesAsync(ct)).First(m => m.Modalidade_ID == evento.Modalidade_ID);
        if (await ConferirAsync(r, modalidade, ct) is { } erro) return erro;
        var professorId = await ProfessorAsync(r, evento.Professor_ID, ct) ?? evento.Professor_ID;

        var inicio = r.Inicio!.Value;
        var fim = inicio.AddMinutes(r.DuracaoMinutos!.Value);
        var alertas = await AlertasAsync(evento.Evento_ID, inicio, fim, professorId, r.LocalId!.Value, ct);
        if (alertas.Count > 0 && !r.ConfirmarAlertas) return PedirConfirmacao(alertas);

        var antes = await CamposAsync(evento, ct);
        var recursos = PoliticaPerfil.Recursos(evento.Tipo, modalidade.Recursos);
        var alteracao = evento.Alterar(inicio, r.DuracaoMinutos.Value, r.LocalId.Value, professorId, r.Adversario, recursos.PermiteAdversario,
            r.Logistica);
        if (!alteracao.Mudou) return ResultadoOperacao.Ok("Nada mudou no jogo.");

        foreach (var (campo, depois) in await CamposAsync(evento, ct)) registro.SeMudou(Entidade, evento.Evento_ID, campo, antes[campo], depois);
        if (alertas.Count > 0) RegistrarAlertas(evento.Evento_ID, alertas);

        var avisadas = 0;
        if (alteracao.AvisaFamilias)
        {
            var convocados = (await eventos.ConvocacoesAsync([evento.Evento_ID], ct))
                .Where(c => c.Disparada && c.Situacao != SituacaoConvocacao.Cancelada).Select(c => c.Aluno_ID).Distinct().ToList();
            if (convocados.Count > 0)
            {
                // O aviso da alteração anterior que ainda está na fila traz o horário velho: sai antes do novo.
                await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemEvento, evento.Evento_ID, "Houve outra alteração do jogo depois.", ct);
                // Cada alteração é um aviso novo: a etapa entra na chave anti-duplicidade, e o sufixo único impede que duas
                // alterações no mesmo segundo se confundam (a segunda sumiria em silêncio).
                var etapa = $"A{relogio.Agora().ToString("yyMMddHHmmss", CultureInfo.InvariantCulture)}{Guid.NewGuid():N}"[..Mensagem.TamanhoEtapa];
                avisadas = await emissor.EventoAlteradoAsync(evento.Evento_ID, await textos.ParaMensagemAsync(evento, ct), convocados, etapa, ct);
            }
        }

        // P45: mudou o horário, a resposta dada para o horário de antes deixa de valer. A convocação volta a "aguardando" e
        // sai de novo, com os botões; a mensagem de antes que ainda estava na fila não sai mais.
        var reconvocadas = 0;
        if (alteracao.MudouInicio && (await parametros.ObterAsync(ct)).Convocacao.NovaConfirmacaoAoMudarHorario)
        {
            var agora = relogio.Agora();
            var etapa = $"N{agora.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture)}{Guid.NewGuid():N}"[..Mensagem.TamanhoEtapa];
            var convocacoes = (await eventos.ConvocacoesAsync([evento.Evento_ID], ct)).Where(c => c.Disparada).ToList();
            var nomes = await eventos.NomesDosAlunosAsync(convocacoes.Select(c => c.Aluno_ID).ToList(), ct);
            var dados = await textos.ParaMensagemAsync(evento, ct);
            foreach (var convocacao in convocacoes.Where(c => c.PedirNovaConfirmacao("O horário do jogo mudou.", agora)))
            {
                await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemConvocacao, convocacao.Convocacao_ID, "O horário do jogo mudou: saiu a convocação nova.", ct);
                await emissor.ConvocacaoAsync(convocacao, dados, nomes.GetValueOrDefault(convocacao.Aluno_ID, "—"), null, ct, etapa);
                reconvocadas++;
            }
        }
        await eventos.SalvarAsync(ct);
        return ResultadoOperacao.Ok("Jogo alterado." + (avisadas > 0 ? $" {avisadas} aviso(s) às famílias na fila." : string.Empty)
                                    + (reconvocadas > 0 ? $" {reconvocadas} convocação(ões) pedindo nova confirmação." : string.Empty));
    }

    /// <summary>O placar do jogo realizado (P46), quando a modalidade tem placar. Corrigir é registrar de novo.</summary>
    public async Task<ResultadoOperacao> RegistrarPlacarAsync(Guid id, int? nos, int? adversario, CancellationToken ct = default)
    {
        if (await ObterNoEscopoAsync(id, Operacao.Alterar, ct) is not { } evento) return ResultadoOperacao.Erro("Jogo não encontrado.");
        if (nos is not { } a || adversario is not { } b || a is < 0 or > Evento.PlacarMaximo || b is < 0 or > Evento.PlacarMaximo)
            return ResultadoOperacao.Erro($"Informe o placar dos dois lados, de 0 a {Evento.PlacarMaximo}.", "PlacarNos");
        if (evento.Situacao is not (SituacaoEvento.Realizado or SituacaoEvento.ResultadoHomologado))
            return ResultadoOperacao.Erro("Marque o jogo como realizado antes de registrar o placar.");
        var modalidade = (await configuracao.ListarModalidadesAsync(ct)).First(m => m.Modalidade_ID == evento.Modalidade_ID);
        if (!PoliticaPerfil.Recursos(evento.Tipo, modalidade.Recursos).PermitePlacar)
            return ResultadoOperacao.Erro("A modalidade deste jogo não tem placar.");
        var antes = evento.PlacarNos is null ? null : $"{evento.PlacarNos} x {evento.PlacarAdversario}";
        var situacaoAntes = Evento.Rotulo(evento.Situacao);
        evento.RegistrarPlacar(a, b, true, relogio.Agora(), usuario.Nome ?? "—");
        registro.Registrar(Entidade, id, "Placar", antes, $"{a} x {b}", null);
        registro.SeMudou(Entidade, id, "Situação", situacaoAntes, Evento.Rotulo(evento.Situacao));
        await eventos.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Placar registrado: {a} x {b}.");
    }

    public async Task<ResultadoOperacao> ConfirmarAsync(Guid id, CancellationToken ct = default)
    {
        if (await ObterNoEscopoAsync(id, Operacao.Alterar, ct) is not { } evento) return ResultadoOperacao.Erro("Jogo não encontrado.");
        if (evento.Situacao != SituacaoEvento.Agendado) return ResultadoOperacao.Erro("Só o jogo agendado é confirmado.");
        evento.Confirmar();
        registro.Registrar(Entidade, id, "Situação", Evento.Rotulo(SituacaoEvento.Agendado), Evento.Rotulo(evento.Situacao), null);
        await eventos.SalvarAsync(ct);
        return ResultadoOperacao.Ok("Jogo confirmado.");
    }

    /// <summary>O jogo aconteceu: a convocação que ainda esperava resposta vira "sem resposta" (P19).</summary>
    public async Task<ResultadoOperacao> RealizarAsync(Guid id, CancellationToken ct = default)
    {
        if (await ObterNoEscopoAsync(id, Operacao.Alterar, ct) is not { } evento) return ResultadoOperacao.Erro("Jogo não encontrado.");
        var agora = relogio.Agora();
        if (!evento.Aberto) return ResultadoOperacao.Erro($"Jogo {Evento.Rotulo(evento.Situacao).ToLowerInvariant()} não pode ser realizado.");
        if (evento.Inicio > agora) return ResultadoOperacao.Erro("O jogo ainda não começou.");
        var antes = Evento.Rotulo(evento.Situacao);
        evento.Realizar(agora);
        foreach (var convocacao in await eventos.ConvocacoesAsync([id], ct)) convocacao.MarcarSemResposta(agora);
        registro.Registrar(Entidade, id, "Situação", antes, Evento.Rotulo(evento.Situacao), null);
        await eventos.SalvarAsync(ct);
        return ResultadoOperacao.Ok("Jogo marcado como realizado.");
    }

    /// <summary>
    /// Cancelar ou adiar (spec §6, BL-M07-13, CA-14): a convocação disparada fica cancelada, com o motivo; a que ainda não
    /// saiu some; o que estava na fila é recolhido; e cada família dos convocados recebe um aviso, urgente se o jogo era nas
    /// próximas 24 horas.
    /// </summary>
    public async Task<ResultadoOperacao> EncerrarAsync(EncerrarEventoRequisicao r, CancellationToken ct = default)
    {
        var validacao = await validadorEncerrar.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await ObterNoEscopoAsync(r.Id, Operacao.Alterar, ct) is not { } evento) return ResultadoOperacao.Erro("Jogo não encontrado.");
        if (!evento.Aberto) return ResultadoOperacao.Erro($"Jogo {Evento.Rotulo(evento.Situacao).ToLowerInvariant()} não muda mais.");

        var agora = relogio.Agora();
        var motivo = r.Motivo!.Trim();
        var antes = Evento.Rotulo(evento.Situacao);
        evento.Encerrar(r.Adiar, motivo, agora, usuario.Nome ?? "—");
        var rotulo = r.Adiar ? "adiado" : "cancelado";

        var avisar = new List<Guid>();
        foreach (var convocacao in await eventos.ConvocacoesAsync([evento.Evento_ID], ct))
        {
            if (!convocacao.Disparada)
            {
                eventos.RemoverConvocacao(convocacao);
                continue;
            }
            if (convocacao.Situacao == SituacaoConvocacao.Cancelada) continue;
            avisar.Add(convocacao.Aluno_ID);
            convocacao.Cancelar($"Jogo {rotulo}: {motivo}", agora);
            await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemConvocacao, convocacao.Convocacao_ID, $"O jogo foi {rotulo}.", ct);
        }
        await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemEvento, evento.Evento_ID, $"O jogo foi {rotulo}.", ct);
        var avisadas = avisar.Count == 0
            ? 0
            : await emissor.EventoCanceladoAsync(evento.Evento_ID, await textos.ParaMensagemAsync(evento, ct), avisar.Distinct().ToList(),
                r.Adiar, motivo, ct);

        registro.Registrar(Entidade, evento.Evento_ID, "Situação", antes, Evento.Rotulo(evento.Situacao), motivo);
        await eventos.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Jogo {rotulo}." +
                                    (avisar.Count > 0 ? $" {avisar.Distinct().Count()} convocação(ões) cancelada(s); {avisadas} aviso(s) às famílias na fila." : string.Empty));
    }

    /// <summary>
    /// Os alertas de agenda (BL-M07-03, RF-CON-018): outro treino ou jogo do professor no horário, fora da disponibilidade,
    /// dentro de uma indisponibilidade; e outro compromisso no mesmo local (P31, EX-02).
    /// </summary>
    public async Task<IReadOnlyList<ConflitoAgenda>> AlertasAsync(Guid? eventoId, DateTime inicio, DateTime fim, Guid professorId, Guid localId,
        CancellationToken ct = default)
    {
        var compromissos = (await agenda.CompromissosAsync(inicio.Date, fim.Date.AddDays(1), null, null, ct))
            .Where(c => c.Id != eventoId).ToList();
        var alertas = new List<ConflitoAgenda>();
        if ((await equipe.ProfessoresComAgendaAsync([professorId], ct)).FirstOrDefault() is { } professor)
            alertas.AddRange(DetectorConflitos.ParaCompromisso(professor, inicio, fim, compromissos.Where(c => c.ProfessorId == professorId)));
        var local = await turmas.ObterLocalAsync(localId, ct);
        alertas.AddRange(DetectorConflitos.LocalOcupado(local?.Nome ?? "O local", inicio, fim, compromissos.Where(c => c.LocalId == localId)));
        if (local is not null) alertas.AddRange(DetectorConflitos.ForaDoHorarioDoLocal(local, inicio, fim));
        return alertas;
    }

    private async Task<ResultadoOperacao?> ConferirAsync(EventoRequisicao r, Modalidade modalidade, CancellationToken ct)
    {
        if (!PoliticaPerfil.TipoSuportado(modalidade.Perfil, TipoEvento.Jogo))
            return ResultadoOperacao.Erro($"A modalidade {modalidade.Nome} não tem jogos.", nameof(r.Turmas));
        if (!string.IsNullOrWhiteSpace(r.Adversario) && !PoliticaPerfil.Recursos(TipoEvento.Jogo, modalidade.Recursos).PermiteAdversario)
            return ResultadoOperacao.Erro($"A modalidade {modalidade.Nome} não tem adversário.", nameof(r.Adversario));
        if (await turmas.ObterLocalAsync(r.LocalId!.Value, ct) is null) return ResultadoOperacao.Erro("Escolha o local.", nameof(r.LocalId));
        if (r.Inicio!.Value < relogio.Agora()) return ResultadoOperacao.Erro("O jogo não pode começar no passado.", nameof(r.Hora));
        return null;
    }

    /// <summary>A gestão escolhe o professor (padrão: o da turma); o professor monta como ele mesmo.</summary>
    private async Task<Guid?> ProfessorAsync(EventoRequisicao r, Guid padrao, CancellationToken ct)
    {
        if (Gestao) return r.ProfessorId is { } escolhido && await equipe.ObterProfessorAsync(escolhido, ct) is not null ? escolhido : padrao;
        return usuario.Id is { } pessoa && await equipe.ProfessorDaPessoaAsync(pessoa, ct) is { } proprio ? proprio.Professor_ID : null;
    }

    private static ResultadoOperacao PedirConfirmacao(IReadOnlyList<ConflitoAgenda> alertas) => new()
    {
        Sucesso = false,
        Mensagem = alertas.Count == 1
            ? "Há um alerta de agenda. Confira e confirme para salvar mesmo assim."
            : $"Há {alertas.Count} alertas de agenda. Confira e confirme para salvar mesmo assim.",
        Campo = nameof(EventoRequisicao.ConfirmarAlertas),
        Dados = new AlertasEvento(alertas)
    };

    private void RegistrarAlertas(Guid eventoId, IReadOnlyList<ConflitoAgenda> alertas) =>
        registro.Registrar(Entidade, eventoId, "Alertas de agenda confirmados", null,
            string.Join(" | ", alertas.Select(a => a.Descricao)), "Quem salvou viu os alertas e decidiu seguir (R9).");

    /// <summary>O que a alteração pode mudar, campo a campo, para o registro de quem mudou o quê (antes e depois).</summary>
    private async Task<Dictionary<string, string?>> CamposAsync(Evento e, CancellationToken ct) => new()
    {
        ["Quando"] = $"{TextosEventos.Quando(e.Inicio)} · {e.DuracaoMinutos} min",
        ["Local"] = (await turmas.ObterLocalAsync(e.Local_ID, ct))?.Nome,
        ["Professor"] = (await equipe.ObterProfessorAsync(e.Professor_ID, ct))?.Nome,
        ["Adversário"] = e.Adversario,
        ["Logística"] = e.Logistica
    };
}
