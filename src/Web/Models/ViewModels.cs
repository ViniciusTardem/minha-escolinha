using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Application.Plataforma;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Auditoria;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Models;

/// <summary>Rodapé de paginação das listas (Views/Shared/_Paginacao.cshtml). <see cref="Parametros"/> mantém a busca nos links.</summary>
public record PaginacaoViewModel(
    int Numero,
    int TotalPaginas,
    int PrimeiroItem,
    int UltimoItem,
    int Total,
    string Rotulo,
    IReadOnlyDictionary<string, string?> Parametros)
{
    public static PaginacaoViewModel De<T>(Pagina<T> pagina, string rotulo, IReadOnlyDictionary<string, string?> parametros) =>
        new(pagina.Numero, pagina.TotalPaginas, pagina.PrimeiroItem, pagina.UltimoItem, pagina.Total, rotulo, parametros);
}

/// <summary>Um passo da configuração da escolinha no painel inicial.</summary>
public record PassoConfiguracao(string Titulo, string Descricao, string Icone, bool Feito, string Url, string Acao);

/// <summary>Algo que pede ação do gestor agora (S2: vínculos a confirmar, mensagens retidas, saldo baixo).</summary>
public record PendenciaPainel(string Titulo, string Texto, string Icone, string Url, string Acao);

public record PainelViewModel(
    string NomeEscolinha,
    string Papeis,
    bool VeConfiguracao,
    IReadOnlyList<PassoConfiguracao> Passos,
    IReadOnlyList<Modulo> ProximasEntregas,
    IReadOnlyList<ItemAgenda> ProximosTreinos,
    IReadOnlyList<(Guid Id, string Nome, DateOnly TrialAte)> TrialsVencendo,
    IReadOnlyList<(Guid Id, string Nome, DateOnly TrialAte)> TrialsVencidos,
    DateOnly Hoje,
    IReadOnlyList<PendenciaPainel> Pendencias);

public record ParametrosViewModel(
    DominioParametro Dominio,
    IReadOnlyList<DominioParametro> Dominios,
    IReadOnlyList<ParametroResposta> Parametros,
    bool PodeAlterar,
    IReadOnlyCollection<string> ChavesComErro);

public record HistoricoParametrosViewModel(string? Chave, IReadOnlyList<RegistroAlteracao> Registros);

/// <summary>Tela de créditos: saldo e histórico, e a compra de pacotes pelo Stripe para quem pode gastar pela escolinha.</summary>
public record CreditosViewModel(CreditosResposta Creditos, TelaCompraCreditos Compra, bool PodeComprar);

/// <summary>Volta do pagamento: a compra e, se ela ainda não terminou, a página acompanha sozinha.</summary>
public record CompraCreditosViewModel(CompraResposta Compra, bool Cancelada, int? Saldo);

/// <summary>Pacotes de créditos (equipe da plataforma) e a situação da integração com o Stripe.</summary>
public record PacotesViewModel(IReadOnlyList<PacoteResposta> Pacotes, IntegracaoStripeResposta Integracao);

/// <summary>Ficha do professor e o formulário da indisponibilidade nova (campos com o prefixo <c>Nova</c>).</summary>
public record FichaProfessorViewModel(FichaProfessor Ficha, IndisponibilidadeRequisicao Nova);

/// <summary>Lista de alunos: a visão por turma e a geral sobre a mesma base (BL-M03-05).</summary>
public record AlunosViewModel(FiltroAlunos Filtro, Pagina<AlunoNaLista> Lista, IReadOnlyList<TurmaResumo> Turmas, DateOnly Hoje);

/// <summary>Lista de responsáveis com a busca.</summary>
public record ResponsaveisViewModel(string? Busca, Pagina<ResponsavelNaLista> Lista);

/// <summary>Log de mensagens do gestor (S2): o filtro, a página e o quadro do motor.</summary>
public record MensagensViewModel(FiltroMensagens Filtro, Pagina<MensagemNaLista> Lista, ResumoMensageria Resumo);

/// <summary>Convites de acesso (S2): a lista filtrada, todos (para os contadores) e os vínculos a confirmar.</summary>
public record ConvitesViewModel(
    IReadOnlyList<CandidatoAcesso> Lista,
    IReadOnlyList<CandidatoAcesso> Todos,
    string? Tipo,
    SituacaoAcesso? Situacao,
    string? Busca,
    int VinculosAConfirmar,
    bool PodeConvidar);

/// <summary>A ativação pelo link: o estado do convite, a identificação conferida e o passo da senha.</summary>
public record AtivacaoViewModel(
    string Token,
    EstadoConvite Estado,
    IdentificacaoConvite? Identificacao,
    IdentificacaoRequisicao Cpf,
    AtivacaoRequisicao? Ativacao);
