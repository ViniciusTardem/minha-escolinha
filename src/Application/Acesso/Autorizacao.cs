using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Acesso;

namespace MinhaEscolinha.Application.Acesso;

public enum TipoAlvoEscopo
{
    Aluno = 10,
    Turma = 20,
    Professor = 30,
    Convocacao = 40,
    Cobranca = 50,
    Sessao = 60,

    /// <summary>A ficha de um responsável (o próprio responsável vê a dele).</summary>
    Responsavel = 70
}

/// <summary>O registro sobre o qual se pede acesso (um aluno, uma turma...).</summary>
public readonly record struct AlvoEscopo(TipoAlvoEscopo Tipo, Guid Id);

/// <summary>
/// Resolve o "escopo próprio" de cada papel: as turmas do professor, os filhos com vínculo verificado do responsável.
/// Cada fatia que cria uma entidade com escopo acrescenta a regra dela no <see cref="ResolvedorEscopoCadastros"/>.
/// </summary>
public interface IResolvedorEscopo
{
    Task<bool> NoEscopoAsync(PapelAtuacao papel, AlvoEscopo alvo, Guid pessoaId, CancellationToken ct = default);
}

/// <summary>
/// Motor de permissões por papel, escopo e sensibilidade (BL-M01-03). Três camadas, todas negando por padrão:
/// <list type="number">
/// <item>ação — <see cref="Pode"/>, usado pelo atributo das telas;</item>
/// <item>dado — <see cref="PodeAsync"/> para um registro específico, e o escopo aplicado nas consultas de lista;</item>
/// <item>campo — dado sensível (R5) só sai no view model de quem <see cref="PodeAsync"/> no recurso sensível.</item>
/// </list>
/// </summary>
public interface IAutorizacao
{
    Guid? EscolinhaId { get; }
    bool AdministradorPlataforma { get; }
    IReadOnlySet<PapelAtuacao> Papeis { get; }

    NivelAcesso Nivel(Recurso recurso);

    /// <summary>Acesso à tela/ação do recurso (a lista mostra só o escopo próprio quando o nível é parcial).</summary>
    bool Pode(Recurso recurso, Operacao operacao);

    /// <summary>Acesso a um registro específico: nível total ou o alvo no escopo próprio de algum papel ativo.</summary>
    Task<bool> PodeAsync(Recurso recurso, Operacao operacao, AlvoEscopo alvo, CancellationToken ct = default);
}

public sealed class Autorizacao(IContextoEscolinha contexto, IUsuarioAtual usuarioAtual, IResolvedorEscopo resolvedor) : IAutorizacao
{
    public Guid? EscolinhaId => contexto.EscolinhaId;
    public bool AdministradorPlataforma => contexto.AdministradorPlataforma;
    public IReadOnlySet<PapelAtuacao> Papeis => contexto.Papeis;

    public NivelAcesso Nivel(Recurso recurso) =>
        contexto.EscolinhaId is null ? NivelAcesso.Nenhum : MatrizPermissoes.MaiorNivel(recurso, contexto.Papeis);

    public bool Pode(Recurso recurso, Operacao operacao) =>
        MatrizPermissoes.Permite(Nivel(recurso), operacao, alvoNoEscopo: true);

    public async Task<bool> PodeAsync(Recurso recurso, Operacao operacao, AlvoEscopo alvo, CancellationToken ct = default)
    {
        if (contexto.EscolinhaId is null || usuarioAtual.Id is not { } pessoaId) return false;

        foreach (var papel in contexto.Papeis.OrderBy(p => p))
        {
            var nivel = MatrizPermissoes.Nivel(recurso, papel);
            if (nivel == NivelAcesso.Total) return true;
            if (!MatrizPermissoes.Permite(nivel, operacao, alvoNoEscopo: true)) continue;
            if (await resolvedor.NoEscopoAsync(papel, alvo, pessoaId, ct)) return true;
        }
        return false;
    }
}
