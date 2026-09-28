using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Eventos;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Hubs;

/// <summary>
/// Tempo real do painel de confirmações (<c>/hubs/convocacoes</c>, BL-M07-09, CA-12), no padrão do <c>AdmissoesHub</c> do DP:
/// o servidor só empurra o id dos eventos que mudaram, e a tela busca o painel por GET (a autorização fica na página, com o
/// escopo). O grupo é o da escolinha da sessão, entrado em <see cref="OnConnectedAsync"/>: depois de uma reconexão, a
/// conexão nova reentra sozinha. Só entra quem vê os jogos (gestão e professores), com os papéis relidos na hora, como em
/// cada requisição (§7); o responsável acompanha pelo portal.
/// </summary>
[Authorize]
public class ConvocacoesHub(IAcessoRepositorio acesso) : Hub
{
    public const string Rota = "/hubs/convocacoes";

    /// <summary>Mensagem ao navegador (nome usado em <c>wwwroot/js/convocacao.js</c>).</summary>
    public const string EventoConvocacoesAlteradas = "ConvocacoesAlteradas";

    public static string Grupo(Guid escolinhaId) => $"escolinha:{escolinhaId:N}";

    /// <summary>Quem acompanha os painéis da escolinha: quem tem algum acesso aos jogos.</summary>
    public static bool Acompanha(IEnumerable<PapelAtuacao> papeis) =>
        MatrizPermissoes.MaiorNivel(Recurso.EventosConvocacao, papeis) != NivelAcesso.Nenhum;

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.PessoaId() is { } pessoa && Context.User.EscolinhaId() is { } escolinha
            && Acompanha(await acesso.PapeisAtivosAsync(pessoa, escolinha, Context.ConnectionAborted)))
            await Groups.AddToGroupAsync(Context.ConnectionId, Grupo(escolinha));
        await base.OnConnectedAsync();
    }
}

/// <summary>Avisa as telas abertas da escolinha. Melhor esforço: a tela também busca de novo a cada minuto.</summary>
public class NotificadorConvocacoesSignalR(IHubContext<ConvocacoesHub> hub, ILogger<NotificadorConvocacoesSignalR> logger)
    : INotificadorConvocacoes
{
    public void EventosAlterados(IReadOnlyCollection<(Guid Escolinha, Guid Evento)> eventos)
    {
        foreach (var escolinha in eventos.GroupBy(e => e.Escolinha))
        {
            var ids = escolinha.Select(e => e.Evento.ToString()).Distinct().ToArray();
            _ = hub.Clients.Group(ConvocacoesHub.Grupo(escolinha.Key))
                .SendAsync(ConvocacoesHub.EventoConvocacoesAlteradas, new { eventos = ids })
                .ContinueWith(t => logger.LogWarning(t.Exception, "Tempo real das convocações: aviso não enviado."),
                    TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
