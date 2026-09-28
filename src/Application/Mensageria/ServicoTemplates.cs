using System.Text.RegularExpressions;
using FluentValidation;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Application.Mensageria;

public sealed record TemplateNaLista(
    Guid Id,
    GatilhoMensagem Gatilho,
    TomMensagem Tom,
    CategoriaTemplate Categoria,
    string NomeNaMeta,
    string Corpo,
    IReadOnlyList<string> Variaveis,
    IReadOnlyList<string> Botoes,
    SituacaoAprovacaoTemplate Situacao,
    string? Observacao,
    DateTime? AtualizadoEm);

public class TemplateRequisicao
{
    public Guid Id { get; set; }
    public string NomeNaMeta { get; set; } = string.Empty;
    public string Corpo { get; set; } = string.Empty;
    public string? Botoes { get; set; }
    public CategoriaTemplate Categoria { get; set; } = CategoriaTemplate.Utilidade;
    public SituacaoAprovacaoTemplate Situacao { get; set; } = SituacaoAprovacaoTemplate.Rascunho;
    public string? Observacao { get; set; }
}

public partial class TemplateRequisicaoValidador : AbstractValidator<TemplateRequisicao>
{
    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex NomeMeta();

    public TemplateRequisicaoValidador()
    {
        RuleFor(x => x.NomeNaMeta).NotEmpty().WithMessage("Informe o nome do template na Meta.")
            .MaximumLength(TemplateMensagem.TamanhoNome)
            .Matches(NomeMeta()).WithMessage("O nome na Meta usa só letras minúsculas, números e sublinhado (ex.: treino_cancelado_v2).");
        RuleFor(x => x.Corpo).NotEmpty().WithMessage("Escreva o texto do template.")
            .MaximumLength(TemplateMensagem.TamanhoCorpo).WithMessage($"O texto pode ter no máximo {TemplateMensagem.TamanhoCorpo} caracteres.")
            .Must(c => !c.TrimStart().StartsWith("{{", StringComparison.Ordinal) && !c.TrimEnd().EndsWith("}}", StringComparison.Ordinal))
            .WithMessage("A Meta recusa template que começa ou termina com um parâmetro: ponha texto antes e depois.");
        RuleFor(x => x.Botoes)
            .Must(b => string.IsNullOrWhiteSpace(b) || b.Split('|').Length <= 3).WithMessage("No máximo 3 botões de resposta.")
            .Must(b => string.IsNullOrWhiteSpace(b) || b.Split('|').All(r => r.Trim().Length is > 0 and <= 25))
            .WithMessage("Cada botão tem de 1 a 25 caracteres (separe os botões com |).");
        RuleFor(x => x.Observacao).MaximumLength(TemplateMensagem.TamanhoObservacao);
        RuleFor(x => x.Categoria).IsInEnum();
        RuleFor(x => x.Situacao).IsInEnum();
    }
}

/// <summary>
/// O catálogo de templates (BL-M09-03), da plataforma: a equipe LDNA acompanha a aprovação na Meta e ajusta os textos.
/// O texto novo de um template aprovado exige nova aprovação: troque o nome na Meta (a versão, _v2) ao mudar o texto.
/// </summary>
public class ServicoTemplates(
    IMensageriaRepositorio repositorio,
    IContextoEscolinha contexto,
    IValidator<TemplateRequisicao> validador)
{
    public async Task<IReadOnlyList<TemplateNaLista>> ListarAsync(CancellationToken ct = default)
    {
        var gravados = await repositorio.ListarTemplatesAsync(ct);
        return gravados
            .OrderBy(t => (int)t.Gatilho).ThenBy(t => t.Tom)
            .Select(t => new TemplateNaLista(t.TemplateMensagem_ID, t.Gatilho, t.Tom, t.Categoria, t.NomeNaMeta, t.Corpo, t.NomesVariaveis,
                t.ListaBotoes, t.Situacao, t.Observacao, t.DataAtualizacao ?? t.DataCadastro))
            .ToList();
    }

    public async Task<TemplateRequisicao?> ParaEdicaoAsync(Guid id, CancellationToken ct = default) =>
        await repositorio.ObterTemplateAsync(id, ct) is { } t
            ? new TemplateRequisicao
            {
                Id = t.TemplateMensagem_ID, NomeNaMeta = t.NomeNaMeta, Corpo = t.Corpo, Botoes = t.Botoes, Categoria = t.Categoria,
                Situacao = t.Situacao, Observacao = t.Observacao
            }
            : null;

    public async Task<ResultadoOperacao> SalvarAsync(TemplateRequisicao r, CancellationToken ct = default)
    {
        if (!contexto.AdministradorPlataforma) return ResultadoOperacao.Erro("Só a equipe da plataforma mexe nos templates.");
        var validacao = await validador.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await repositorio.ObterTemplateAsync(r.Id, ct) is not { } template) return ResultadoOperacao.Erro("Template não encontrado.");

        var esperados = Enumerable.Range(1, template.NomesVariaveis.Count).ToList();
        if (!RenderizadorTemplate.Marcadores(r.Corpo).SequenceEqual(esperados))
            return ResultadoOperacao.Erro(
                $"O texto precisa usar {{{{1}}}} a {{{{{esperados.Count}}}}}, cada um uma vez e em ordem ({string.Join(", ", template.NomesVariaveis)}).",
                nameof(TemplateRequisicao.Corpo));

        var textoMudou = template.Corpo != r.Corpo.Trim() || (template.Botoes ?? "") != (r.Botoes?.Trim() ?? "");
        if (textoMudou && template.Situacao == SituacaoAprovacaoTemplate.Aprovado && template.NomeNaMeta == r.NomeNaMeta.Trim())
            return ResultadoOperacao.Erro("Texto novo precisa de nova aprovação: troque o nome na Meta (ex.: _v2) e volte a situação para rascunho.",
                nameof(TemplateRequisicao.NomeNaMeta));

        // P55: a mensagem guarda o nome do template com que foi montada. Trocada a versão de um aprovado, o nome anterior
        // continua aceito no envio, para o que já está na fila sair pelo template antigo, que segue aprovado na Meta.
        if (template.Situacao == SituacaoAprovacaoTemplate.Aprovado && template.NomeNaMeta != r.NomeNaMeta.Trim())
            template.NomeAnterior = template.NomeNaMeta;
        template.NomeNaMeta = r.NomeNaMeta.Trim();
        template.Corpo = r.Corpo.Trim();
        template.Botoes = string.IsNullOrWhiteSpace(r.Botoes) ? null : string.Join("|", r.Botoes.Split('|').Select(b => b.Trim()));
        template.Categoria = r.Categoria;
        template.Situacao = r.Situacao;
        template.Observacao = string.IsNullOrWhiteSpace(r.Observacao) ? null : r.Observacao.Trim();
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Template {template.NomeNaMeta} salvo ({TemplateMensagem.Rotulo(template.Situacao)}).");
    }

    /// <summary>
    /// Startup: grava os textos padrão que ainda não estão no catálogo e atualiza o rascunho que nenhuma pessoa editou
    /// (idempotente; o template editado ou submetido nunca é sobrescrito).
    /// </summary>
    public async Task<int> ReconciliarAsync(CancellationToken ct = default)
    {
        var existentes = (await repositorio.ListarTemplatesAsync(ct)).ToDictionary(t => (t.Gatilho, t.Tom));
        var mudancas = 0;
        foreach (var padrao in TemplatesPadrao.Todos)
        {
            if (existentes.TryGetValue((padrao.Gatilho, padrao.Tom), out var gravado))
            {
                if (TemplatesPadrao.AtualizarRascunho(gravado, padrao)
                    && await repositorio.ObterTemplateAsync(gravado.TemplateMensagem_ID, ct) is { } rastreado
                    && TemplatesPadrao.AtualizarRascunho(rastreado, padrao))
                    mudancas++;
                continue;
            }
            repositorio.AdicionarTemplate(TemplatesPadrao.Criar(padrao));
            mudancas++;
        }
        if (mudancas > 0) await repositorio.SalvarAsync(ct);
        return mudancas;
    }
}
