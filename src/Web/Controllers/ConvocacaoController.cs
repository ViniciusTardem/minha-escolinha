using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Eventos;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;
using MinhaEscolinha.Web.Models;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Jogos e convocações (S5), na quadra: a montagem do fim de semana numa tela só — o jogo novo, a escolha dos convocados de
/// cada jogo e o disparo de tudo (BL-M07-08, CA-10) — e o painel de confirmações de cada jogo, que atualiza sozinho
/// (BL-M07-09, CA-12). A gestão vê todos os jogos; o professor, os das turmas dele (BL-M07-18). Nenhuma ação fala com o
/// WhatsApp no request: as mensagens entram na fila do motor (regra de ouro 2).
/// </summary>
[Permissao(Recurso.EventosConvocacao)]
public class ConvocacaoController(ServicoConvocacoes convocacoes, ServicoEventos eventos) : Controller
{
    public const string Modulo = "jogos";

    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? semana, CancellationToken ct)
    {
        if (await convocacoes.SemanaAsync(semana, ct) is not { } tela) return Forbid();
        return View(new SemanaConvocacoesViewModel(tela, NovoJogo(tela), AbrirNovo: false));
    }

    [HttpPost]
    [Permissao(Recurso.EventosConvocacao, Operacao.Alterar)]
    public async Task<IActionResult> Novo(EventoRequisicao requisicao, DateOnly? semana, CancellationToken ct)
    {
        requisicao.Id = null;
        var resultado = await eventos.CriarAsync(requisicao, ct);
        if (resultado.Sucesso)
        {
            TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
            return Redirect($"/Convocacao?semana={requisicao.Data:yyyy-MM-dd}#jogo-{resultado.Dados}");
        }

        ModelState.AdicionarErro(resultado);
        if (await convocacoes.SemanaAsync(semana ?? requisicao.Data, ct) is not { } tela) return Forbid();
        var formulario = new FormularioJogoViewModel(requisicao, tela.Formulario, (resultado.Dados as AlertasEvento)?.Alertas, Alteracao: false);
        return View("Index", new SemanaConvocacoesViewModel(tela, formulario, AbrirNovo: true));
    }

    /// <summary>O mesmo formulário salva a escolha dos convocados ou salva e dispara tudo (botão "Disparar").</summary>
    [HttpPost]
    [Permissao(Recurso.EventosConvocacao, Operacao.Alterar)]
    public async Task<IActionResult> Selecao([FromForm(Name = "eventos")] List<Guid>? eventosDaTela, [FromForm(Name = "selecao")] List<string>? selecao,
        bool disparar, DateOnly? semana, CancellationToken ct)
    {
        var ids = eventosDaTela ?? [];
        var marcados = (selecao ?? []).Select(Ler).OfType<(Guid Evento, Guid Aluno)>().ToList();
        var salvo = await convocacoes.SalvarSelecaoAsync(ids, marcados, ct);
        if (salvo.Sucesso && disparar)
        {
            var disparo = await convocacoes.DispararAsync(ids, ct);
            TempData[disparo.Sucesso ? ChavesTempData.Sucesso : ChavesTempData.Erro] = disparo.Mensagem;
        }
        else
        {
            TempData[salvo.Sucesso ? ChavesTempData.Sucesso : ChavesTempData.Erro] = salvo.Mensagem;
        }
        return Redirect($"/Convocacao?semana={semana:yyyy-MM-dd}");
    }

    [HttpGet]
    public async Task<IActionResult> Jogo(Guid id, CancellationToken ct) =>
        await convocacoes.PainelAsync(id, ct) is { } painel ? View(painel) : NotFound();

    /// <summary>O painel sem o layout: a tela aberta busca de novo quando o tempo real avisa (CA-12).</summary>
    [HttpGet]
    public async Task<IActionResult> Painel(Guid id, CancellationToken ct) =>
        await convocacoes.PainelAsync(id, ct) is { } painel ? PartialView("_Painel", painel) : NotFound();

    /// <summary>A resposta que veio por fora (conversa, telefone): o professor ou o gestor registra, com autoria.</summary>
    [HttpPost]
    [Permissao(Recurso.ResponderConvocacao, Operacao.Alterar)]
    public async Task<IActionResult> Responder(Guid id, Guid convocacao, bool vai, CancellationToken ct)
    {
        var resultado = await convocacoes.RegistrarPorForaAsync(convocacao, vai, ct);
        TempData[resultado.Sucesso ? ChavesTempData.Sucesso : ChavesTempData.Erro] = resultado.Mensagem;
        return Redirect($"/Convocacao/Jogo/{id}");
    }

    [HttpGet]
    [Permissao(Recurso.EventosConvocacao, Operacao.Alterar)]
    public async Task<IActionResult> Alterar(Guid id, CancellationToken ct)
    {
        if (await eventos.ObterNoEscopoAsync(id, Operacao.Alterar, ct) is not { Aberto: true } evento) return NotFound();
        var requisicao = new EventoRequisicao
        {
            Id = id,
            Turmas = [.. evento.IdsDasTurmas],
            Data = DateOnly.FromDateTime(evento.Inicio),
            Hora = TimeOnly.FromDateTime(evento.Inicio),
            DuracaoMinutos = evento.DuracaoMinutos,
            LocalId = evento.Local_ID,
            ProfessorId = evento.Professor_ID,
            Adversario = evento.Adversario,
            Logistica = evento.Logistica
        };
        return View(new FormularioJogoViewModel(requisicao, await eventos.FormularioAsync(ct), null, Alteracao: true));
    }

    [HttpPost]
    [Permissao(Recurso.EventosConvocacao, Operacao.Alterar)]
    public async Task<IActionResult> Alterar(EventoRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await eventos.AlterarAsync(requisicao, ct);
        if (resultado.Sucesso)
        {
            TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
            return Redirect($"/Convocacao/Jogo/{requisicao.Id}");
        }
        ModelState.AdicionarErro(resultado);
        return View(new FormularioJogoViewModel(requisicao, await eventos.FormularioAsync(ct), (resultado.Dados as AlertasEvento)?.Alertas,
            Alteracao: true));
    }

    /// <summary>Cancelar ou adiar (<c>adiar=true</c>): pede o motivo, que vai na mensagem às famílias.</summary>
    [HttpGet]
    [Permissao(Recurso.EventosConvocacao, Operacao.Alterar)]
    public async Task<IActionResult> Encerrar(Guid id, bool adiar, CancellationToken ct)
    {
        if (await convocacoes.PainelAsync(id, ct) is not { PodeAlterar: true } painel) return NotFound();
        return View(new EncerrarJogoViewModel(new EncerrarEventoRequisicao { Id = id, Adiar = adiar }, painel));
    }

    [HttpPost]
    [Permissao(Recurso.EventosConvocacao, Operacao.Alterar)]
    public async Task<IActionResult> Encerrar(EncerrarEventoRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await eventos.EncerrarAsync(requisicao, ct);
        if (resultado.Sucesso)
        {
            TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
            return Redirect($"/Convocacao/Jogo/{requisicao.Id}");
        }
        ModelState.AdicionarErro(resultado);
        return await convocacoes.PainelAsync(requisicao.Id, ct) is { } painel
            ? View(new EncerrarJogoViewModel(requisicao, painel))
            : NotFound();
    }

    [HttpPost]
    [Permissao(Recurso.EventosConvocacao, Operacao.Alterar)]
    public async Task<IActionResult> Confirmar(Guid id, CancellationToken ct) => Voltar(await eventos.ConfirmarAsync(id, ct), id);

    [HttpPost]
    [Permissao(Recurso.EventosConvocacao, Operacao.Alterar)]
    public async Task<IActionResult> Realizar(Guid id, CancellationToken ct) => Voltar(await eventos.RealizarAsync(id, ct), id);

    /// <summary>O placar do jogo realizado (P46).</summary>
    [HttpPost]
    [Permissao(Recurso.EventosConvocacao, Operacao.Alterar)]
    public async Task<IActionResult> Placar(Guid id, int? nos, int? adversario, CancellationToken ct) =>
        Voltar(await eventos.RegistrarPlacarAsync(id, nos, adversario, ct), id);

    private IActionResult Voltar(Application.Comum.ResultadoOperacao resultado, Guid id)
    {
        TempData[resultado.Sucesso ? ChavesTempData.Sucesso : ChavesTempData.Erro] = resultado.Mensagem;
        return Redirect($"/Convocacao/Jogo/{id}");
    }

    /// <summary>O jogo novo começa no sábado da semana, às 09:00, com a duração da modalidade.</summary>
    private static FormularioJogoViewModel NovoJogo(SemanaConvocacoes tela) =>
        new(new EventoRequisicao
        {
            Data = tela.Inicio.AddDays(5),
            Hora = new TimeOnly(9, 0),
            DuracaoMinutos = tela.Formulario.DuracaoPadrao,
            Turmas = tela.Formulario.Turmas.Count == 1 ? [tela.Formulario.Turmas[0].Id] : [],
            LocalId = tela.Formulario.Turmas.Count == 1 ? tela.Formulario.Turmas[0].LocalId : null
        }, tela.Formulario, null, Alteracao: false);

    /// <summary>"{evento:N}:{aluno:N}", a caixa de um aluno num jogo.</summary>
    private static (Guid Evento, Guid Aluno)? Ler(string valor)
    {
        var partes = valor.Split(':');
        return partes.Length == 2 && Guid.TryParse(partes[0], out var evento) && Guid.TryParse(partes[1], out var aluno)
            ? (evento, aluno)
            : null;
    }
}
