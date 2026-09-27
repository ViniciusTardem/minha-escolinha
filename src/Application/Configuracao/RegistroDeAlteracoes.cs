using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Auditoria;

namespace MinhaEscolinha.Application.Configuracao;

/// <summary>Monta as linhas do <see cref="RegistroAlteracao"/> com autor, data e IP da ação atual.</summary>
public sealed class RegistroDeAlteracoes(IConfiguracaoRepositorio repositorio, IContextoEscolinha contexto,
    IUsuarioAtual usuarioAtual, IRelogioBrasil relogio)
{
    /// <summary>Registra só se o valor mudou. Devolve se registrou.</summary>
    public bool SeMudou(string entidade, Guid? registroId, string campo, string? antes, string? depois, string? motivo = null)
    {
        if (string.Equals(antes ?? string.Empty, depois ?? string.Empty, StringComparison.Ordinal)) return false;
        Registrar(entidade, registroId, campo, antes, depois, motivo);
        return true;
    }

    public void Registrar(string entidade, Guid? registroId, string campo, string? antes, string? depois, string? motivo = null)
    {
        var escolinhaId = contexto.EscolinhaId
            ?? throw new InvalidOperationException("Registro de alteração sem escolinha no contexto.");
        repositorio.Registrar(RegistroAlteracao.Novo(escolinhaId, entidade, registroId, campo, antes, depois,
            relogio.Agora(), usuarioAtual.Id, usuarioAtual.Nome, usuarioAtual.Ip, motivo));
    }
}
