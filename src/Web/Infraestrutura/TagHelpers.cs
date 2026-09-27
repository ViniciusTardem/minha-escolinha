using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace MinhaEscolinha.Web.Infraestrutura;

/// <summary>
/// Campo com erro no ModelState ganha <c>aria-invalid="true"</c>: o KTUI pinta a borda (kt-input, kt-select,
/// kt-checkbox), o leitor de tela anuncia o erro e o formulário em etapas (js/assistente.js) abre na etapa do campo.
/// Vale para todo input/select/textarea com asp-for, sem mexer nas Views.
/// </summary>
[HtmlTargetElement("input", Attributes = "asp-for")]
[HtmlTargetElement("select", Attributes = "asp-for")]
[HtmlTargetElement("textarea", Attributes = "asp-for")]
public class CampoInvalidoTagHelper : TagHelper
{
    [HtmlAttributeName("asp-for")]
    public ModelExpression For { get; set; } = default!;

    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var nome = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
        if (ViewContext.ViewData.ModelState.GetFieldValidationState(nome) == ModelValidationState.Invalid)
            output.Attributes.SetAttribute("aria-invalid", "true");
    }
}

/// <summary>
/// Selo de status: <c>&lt;selo-status valor="@Model.Situacao"&gt;&lt;/selo-status&gt;</c>. A cor sai do mapa central
/// (<see cref="ApresentacaoStatus"/>), nunca da View. <c>texto</c> troca só o rótulo; <c>class</c> acrescenta
/// classes de layout (ex.: <c>self-start</c>), nunca de cor.
/// </summary>
[HtmlTargetElement("selo-status", Attributes = "valor")]
public class SeloStatusTagHelper : TagHelper
{
    public Enum? Valor { get; set; }
    public string? Texto { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        if (Valor is null)
        {
            output.SuppressOutput();
            return;
        }
        var (rotulo, variante) = ApresentacaoStatus.Selo(Valor);
        var classes = $"kt-badge kt-badge-sm kt-badge-outline {ApresentacaoStatus.ClasseVariante(variante)}";
        if (output.Attributes.TryGetAttribute("class", out var layout)) classes += " " + layout.Value;
        output.Attributes.SetAttribute("class", classes);
        output.Content.SetContent(Texto ?? rotulo);
    }
}

/// <summary>
/// Cabeçalho padrão de toda tela: título, descrição do que a tela faz, linha meta opcional, link "voltar" e o conteúdo
/// interno como ações à direita. SEMPRE feche a tag (<c>&lt;cabecalho-pagina ...&gt;&lt;/cabecalho-pagina&gt;</c>):
/// auto-fechada, o conteúdo seguinte vira filho (armadilha documentada no guia do design system).
/// </summary>
[HtmlTargetElement("cabecalho-pagina", Attributes = "titulo")]
public class CabecalhoPaginaTagHelper(HtmlEncoder codificador) : TagHelper
{
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string? Meta { get; set; }
    public string? VoltarHref { get; set; }
    public string? VoltarTexto { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var acoes = (await output.GetChildContentAsync()).GetContent();

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "flex flex-wrap items-center lg:items-end justify-between gap-5 pb-7.5");

        var html = new System.Text.StringBuilder();
        html.Append("<div class=\"flex flex-col justify-center gap-2 min-w-0\">");
        if (!string.IsNullOrEmpty(VoltarHref))
        {
            html.Append("<a class=\"kt-link text-sm\" href=\"").Append(codificador.Encode(VoltarHref)).Append("\">")
                .Append("<i class=\"ki-filled ki-arrow-left\"></i> ")
                .Append(codificador.Encode(VoltarTexto ?? "Voltar")).Append("</a>");
        }
        html.Append("<h1 class=\"text-xl font-medium leading-none text-mono\">").Append(codificador.Encode(Titulo)).Append("</h1>");
        if (!string.IsNullOrEmpty(Descricao))
            html.Append("<div class=\"text-sm text-secondary-foreground\">").Append(codificador.Encode(Descricao)).Append("</div>");
        if (!string.IsNullOrEmpty(Meta))
            html.Append("<div class=\"text-xs text-muted-foreground\">").Append(codificador.Encode(Meta)).Append("</div>");
        html.Append("</div>");
        if (!string.IsNullOrWhiteSpace(acoes))
            html.Append("<div class=\"flex flex-wrap items-center gap-2.5\">").Append(acoes).Append("</div>");

        output.Content.SetHtmlContent(html.ToString());
    }
}

/// <summary>
/// Estado vazio padrão: ícone num círculo, título, texto e (conteúdo interno) a ação.
/// <c>&lt;estado-vazio icone="ki-people" titulo="..." texto="..."&gt;&lt;a ...&gt;Ação&lt;/a&gt;&lt;/estado-vazio&gt;</c>.
/// </summary>
[HtmlTargetElement("estado-vazio", Attributes = "titulo")]
public class EstadoVazioTagHelper(HtmlEncoder codificador) : TagHelper
{
    public string Icone { get; set; } = "ki-information-2";
    public string Titulo { get; set; } = string.Empty;
    public string? Texto { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var acao = (await output.GetChildContentAsync()).GetContent();

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "flex flex-col items-center justify-center gap-3 py-12 px-5 text-center");

        var html = new System.Text.StringBuilder();
        html.Append("<div class=\"size-16 rounded-full bg-muted/70 flex items-center justify-center\">")
            .Append("<i class=\"ki-filled ").Append(codificador.Encode(Icone)).Append(" text-3xl text-muted-foreground\"></i></div>");
        html.Append("<h3 class=\"text-base font-semibold text-mono\">").Append(codificador.Encode(Titulo)).Append("</h3>");
        if (!string.IsNullOrEmpty(Texto))
            html.Append("<p class=\"text-sm text-secondary-foreground max-w-md\">").Append(codificador.Encode(Texto)).Append("</p>");
        if (!string.IsNullOrWhiteSpace(acao)) html.Append("<div class=\"mt-1\">").Append(acao).Append("</div>");

        output.Content.SetHtmlContent(html.ToString());
    }
}
