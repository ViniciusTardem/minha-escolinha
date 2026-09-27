using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;

namespace MinhaEscolinha.Web.Infraestrutura;

/// <summary>
/// Monta o <see cref="IContextoEscolinha"/> de cada requisição: a escolinha escolhida (claim do cookie) e os papéis
/// ATIVOS da pessoa nela, relidos do banco a cada requisição — revogar um vínculo nega já na próxima requisição (§7).
/// Sem vínculo ativo na escolinha do cookie, o contexto fica sem escolinha e as telas da escolinha mandam escolher de novo.
/// </summary>
public class ContextoEscolinhaMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, IContextoEscolinha contexto, IAcessoRepositorio acesso)
    {
        var usuario = http.User;
        if (usuario.Identity?.IsAuthenticated == true && usuario.PessoaId() is { } pessoaId)
        {
            var plataforma = usuario.EhPlataforma();
            if (usuario.EscolinhaId() is { } escolinhaId)
            {
                var papeis = await acesso.PapeisAtivosAsync(pessoaId, escolinhaId, http.RequestAborted);
                if (papeis.Count > 0) contexto.Definir(escolinhaId, papeis, plataforma);
                else contexto.DefinirSemEscolinha(plataforma);
            }
            else
            {
                contexto.DefinirSemEscolinha(plataforma);
            }
        }

        await next(http);
    }
}
