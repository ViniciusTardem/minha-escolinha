using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Domain.Integracao;
using MinhaEscolinha.Domain.Plataforma;

namespace MinhaEscolinha.Web.Infraestrutura;

/// <summary>
/// Erro de caso de uso no formulário. Com <see cref="ResultadoOperacao.Campo"/>, o campo fica marcado
/// (<see cref="CampoInvalidoTagHelper"/>) e o formulário em etapas abre na etapa dele; sem campo, só o resumo do topo.
/// </summary>
public static class ErrosDeFormulario
{
    public static void AdicionarErro(this ModelStateDictionary modelState, ResultadoOperacao resultado) =>
        modelState.AddModelError(resultado.Campo ?? string.Empty, resultado.Mensagem);
}

/// <summary>Chaves de TempData lidas pelo layout (viram toast KTUI por JS em arquivo).</summary>
public static class ChavesTempData
{
    public const string Sucesso = "Sucesso";
    public const string Erro = "Erro";
}

public enum VarianteSelo
{
    Neutro,
    Sucesso,
    Info,
    Alerta,
    Perigo,
    Primario
}

/// <summary>
/// Estados que a tela sinaliza sem haver enum de domínio por trás (a conta existe ou não, o saldo passou do aviso).
/// Passam pelo mesmo mapa central: o mesmo estado tem a mesma cor em qualquer tela.
/// </summary>
public enum SinalTela
{
    ContaRecebimentoVinculada,
    ContaRecebimentoAusente,
    SaldoCreditosBaixo,
    PacoteAVenda,
    PacoteForaDeVenda
}

/// <summary>
/// Mapa central estado → selo (docs/design/GUIA-DESIGN-SYSTEM.md §4). A View nunca escolhe a cor de um status:
/// usa <c>&lt;selo-status valor="..."&gt;</c>, que passa por aqui. Cada fatia acrescenta as máquinas de estado dela.
/// </summary>
public static class ApresentacaoStatus
{
    public static (string Rotulo, VarianteSelo Variante) Selo(Enum valor) => valor switch
    {
        SituacaoVinculo.Ativo => ("Ativo", VarianteSelo.Sucesso),
        SituacaoVinculo.Revogado => ("Revogado", VarianteSelo.Neutro),

        AmbienteAsaas.Sandbox => ("Sandbox", VarianteSelo.Info),
        AmbienteAsaas.Producao => ("Produção", VarianteSelo.Sucesso),

        TipoMovimentoCredito.Concessao => ("Concessão", VarianteSelo.Sucesso),
        TipoMovimentoCredito.Consumo => ("Consumo", VarianteSelo.Neutro),
        TipoMovimentoCredito.Estorno => ("Estorno", VarianteSelo.Info),
        TipoMovimentoCredito.Ajuste => ("Ajuste", VarianteSelo.Alerta),

        PapelAtuacao.Gestor => ("Gestor", VarianteSelo.Primario),
        PapelAtuacao.Professor => ("Professor", VarianteSelo.Info),
        PapelAtuacao.Responsavel => ("Responsável", VarianteSelo.Neutro),

        SituacaoCompraCreditos.AguardandoPagamento => ("Aguardando pagamento", VarianteSelo.Info),
        SituacaoCompraCreditos.AguardandoConfirmacao => ("Boleto ou Pix emitido", VarianteSelo.Alerta),
        SituacaoCompraCreditos.Paga => ("Paga", VarianteSelo.Sucesso),
        SituacaoCompraCreditos.Expirada => ("Expirada", VarianteSelo.Neutro),
        SituacaoCompraCreditos.Falhou => ("Falhou", VarianteSelo.Perigo),

        SituacaoWebhook.Recebido => ("Recebido", VarianteSelo.Info),
        SituacaoWebhook.Processado => ("Processado", VarianteSelo.Sucesso),
        SituacaoWebhook.Ignorado => ("Ignorado", VarianteSelo.Neutro),
        SituacaoWebhook.Falhou => ("Falhou", VarianteSelo.Perigo),

        SinalTela.ContaRecebimentoVinculada => ("Conta vinculada", VarianteSelo.Sucesso),
        SinalTela.ContaRecebimentoAusente => ("Sem conta", VarianteSelo.Alerta),
        SinalTela.SaldoCreditosBaixo => ("Saldo baixo", VarianteSelo.Alerta),
        SinalTela.PacoteAVenda => ("À venda", VarianteSelo.Sucesso),
        SinalTela.PacoteForaDeVenda => ("Fora de venda", VarianteSelo.Neutro),

        _ => (valor.ToString(), VarianteSelo.Neutro)
    };

    public static string ClasseVariante(VarianteSelo variante) => variante switch
    {
        VarianteSelo.Sucesso => "kt-badge-success",
        VarianteSelo.Info => "kt-badge-info",
        VarianteSelo.Alerta => "kt-badge-warning",
        VarianteSelo.Perigo => "kt-badge-destructive",
        VarianteSelo.Primario => "kt-badge-primary",
        _ => "kt-badge-secondary"
    };
}

public static class Formatacao
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <summary>"agora", "há 5 min", "há 3 h", "ontem", "há 4 dias" ou a data — ambos em horário de Brasília.</summary>
    public static string TempoRelativo(DateTime quando, DateTime agora)
    {
        var diferenca = agora - quando;
        if (diferenca < TimeSpan.FromMinutes(1)) return "agora";
        if (diferenca < TimeSpan.FromHours(1)) return $"há {(int)diferenca.TotalMinutes} min";
        if (diferenca < TimeSpan.FromHours(24) && quando.Date == agora.Date) return $"há {(int)diferenca.TotalHours} h";
        if (quando.Date == agora.Date.AddDays(-1)) return "ontem";
        if (diferenca < TimeSpan.FromDays(7)) return $"há {Math.Max(2, (int)diferenca.TotalDays)} dias";
        return quando.ToString("dd/MM/yyyy", PtBr);
    }

    /// <summary>"+5543987654321" → "(43) 98765-4321". Outro formato volta como veio.</summary>
    public static string Telefone(string? e164)
    {
        if (string.IsNullOrEmpty(e164)) return string.Empty;
        var n = e164.StartsWith("+55", StringComparison.Ordinal) ? e164[3..] : e164;
        return n.Length switch
        {
            11 => $"({n[..2]}) {n[2..7]}-{n[7..]}",
            10 => $"({n[..2]}) {n[2..6]}-{n[6..]}",
            _ => e164
        };
    }

    public static string Cnpj(string? digitos) => Domain.Comum.Cnpj.Formatar(digitos);

    /// <summary>
    /// Valor de parâmetro (texto invariável gravado) como o gestor lê: "2,5 %", "ligado", "21:00 às 07:00",
    /// "1, 5, 10 dias" ou o rótulo da opção. Usado no editor e no histórico.
    /// </summary>
    public static string Parametro(DefinicaoParametro definicao, string? valor, string seVazio = "—")
    {
        if (valor is null) return seVazio;
        switch (definicao.Tipo)
        {
            case TipoParametro.Booleano:
                return valor == "true" ? "ligado" : "desligado";
            case TipoParametro.Escolha:
                return definicao.Opcoes?.FirstOrDefault(o => o.Valor == valor)?.Rotulo ?? valor;
            case TipoParametro.Janela when JanelaHorario.TentarLer(valor, out var janela):
                return janela.ParaExibicao();
            case TipoParametro.ListaDias:
                valor = valor.Replace(",", ", ");
                break;
            default:
                valor = valor.Replace('.', ',');
                break;
        }
        return definicao.Unidade is null ? valor : $"{valor} {definicao.Unidade}";
    }

    public static string Cep(string? digitos) => digitos is { Length: 8 } ? $"{digitos[..5]}-{digitos[5..]}" : digitos ?? string.Empty;

    public static string DataHora(DateTime? quando) => quando?.ToString("dd/MM/yyyy HH:mm", PtBr) ?? "—";

    public static string Data(DateTime? quando) => quando?.ToString("dd/MM/yyyy", PtBr) ?? "—";

    public static string Numero(int valor) => valor.ToString("N0", PtBr);

    /// <summary>"R$ 1.234,50".</summary>
    public static string Moeda(decimal valor) => valor.ToString("C", PtBr);
}
