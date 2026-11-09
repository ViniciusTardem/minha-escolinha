using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Testes.Arquitetura;

/// <summary>Regras de ouro do CLAUDE.md §4 e regras duras de arquitetura verificadas no código-fonte.</summary>
public partial class RegrasDeOuroTestes
{
    [GeneratedRegex(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b")]
    private static partial Regex RelogioDoServidor();

    /// <summary>
    /// Arquivos autorizados a ignorar o filtro de escolinha (leituras que atravessam escolinhas de propósito).
    /// Acrescentar um arquivo aqui é decisão registrada em docs/DECISOES-EM-ABERTO.md, nunca atalho.
    /// </summary>
    private static readonly string[] ListaPermitidaIgnorarFiltro =
    [
        Path.Combine("Infrastructure", "Acesso", "AcessoRepositorio.cs"),
        Path.Combine("Infrastructure", "Configuracao", "ConsultasPlataforma.cs")
    ];

    [Fact]
    public void Nenhum_codigo_usa_o_relogio_do_servidor()
    {
        var violacoes = LinhasDoCodigo()
            .Where(l => RelogioDoServidor().IsMatch(l.Linha))
            .Select(l => $"{l.Arquivo}:{l.Numero}")
            .ToList();

        Assert.True(violacoes.Count == 0,
            "Use DataHoraBrasil/IRelogioBrasil (ou TimeProvider) em vez de DateTime.Now/UtcNow/Today: " + string.Join(", ", violacoes));
    }

    [Fact]
    public void IgnoreQueryFilters_so_nos_arquivos_da_lista_permitida()
    {
        var violacoes = LinhasDoCodigo()
            .Where(l => l.Linha.Contains("IgnoreQueryFilters", StringComparison.Ordinal))
            .Where(l => !ListaPermitidaIgnorarFiltro.Any(p => l.Arquivo.EndsWith(p, StringComparison.OrdinalIgnoreCase)))
            .Select(l => $"{l.Arquivo}:{l.Numero}")
            .ToList();

        Assert.True(violacoes.Count == 0,
            "Dado de escolinha só é lido fora do filtro nos arquivos da lista permitida (multi-escolinha): " + string.Join(", ", violacoes));
    }

    /// <summary>R3 e regra de ouro 11: um único motor envia, e só o emissor do catálogo de gatilhos cria mensagem.</summary>
    [Fact]
    public void So_o_motor_envia_e_so_o_emissor_de_gatilhos_cria_mensagem()
    {
        var envios = LinhasDoCodigo()
            .Where(l => l.Linha.Contains(".EnviarTemplateAsync(", StringComparison.Ordinal))
            .Where(l => !l.Arquivo.EndsWith(Path.Combine("Application", "Mensageria", "ServicoEnvioMensagens.cs"), StringComparison.OrdinalIgnoreCase))
            .Select(l => $"{l.Arquivo}:{l.Numero}")
            .ToList();
        var criacoes = LinhasDoCodigo()
            .Where(l => CriacaoDeMensagem().IsMatch(l.Linha))
            .Where(l => !l.Arquivo.EndsWith(Path.Combine("Application", "Mensageria", "EmissorGatilhos.cs"), StringComparison.OrdinalIgnoreCase))
            .Select(l => $"{l.Arquivo}:{l.Numero}")
            .ToList();

        Assert.True(envios.Count == 0, "Só o ServicoEnvioMensagens chama o gateway do WhatsApp (R3): " + string.Join(", ", envios));
        Assert.True(criacoes.Count == 0,
            "Mensagem só nasce no EmissorGatilhos, a partir de uma regra do catálogo (R3): " + string.Join(", ", criacoes));
    }

    [GeneratedRegex(@"\bMensagem\.Nova\(")]
    private static partial Regex CriacaoDeMensagem();

    [Fact]
    public void Toda_action_declara_o_acesso()
    {
        var marcadores = new[]
        {
            typeof(AllowAnonymousAttribute), typeof(PermissaoAttribute), typeof(PlataformaAttribute), typeof(SomenteAutenticadoAttribute)
        };
        var semDeclaracao = AcoesDosControllers()
            .Where(x => !marcadores.Any(a => x.Controlador.IsDefined(a, true) || x.Acao.IsDefined(a, true)))
            .Select(x => $"{x.Controlador.Name}.{x.Acao.Name}")
            .ToList();

        Assert.True(semDeclaracao.Count == 0,
            "Toda action precisa de [Permissao], [Plataforma], [SomenteAutenticado] ou [AllowAnonymous] (nega por padrão): "
            + string.Join(", ", semDeclaracao));
    }

    /// <summary>
    /// Sem nome explícito, quando o formulário não traz o prefixo do parâmetro o ASP.NET liga a coleção com prefixo
    /// vazio e ela recebe todos os campos do formulário (achado na validação do S0: a aba de parâmetros sem janela
    /// mandava "dominio" e o token antiforgery como parâmetros).
    /// </summary>
    [Fact]
    public void Parametro_colecao_de_action_declara_o_nome_do_binding()
    {
        var semNome = AcoesDosControllers()
            .SelectMany(x => x.Acao.GetParameters().Select(p => (x.Controlador, x.Acao, Parametro: p)))
            .Where(x => x.Parametro.ParameterType != typeof(string)
                        && typeof(System.Collections.IEnumerable).IsAssignableFrom(x.Parametro.ParameterType))
            .Where(x => !x.Parametro.GetCustomAttributes().OfType<IModelNameProvider>().Any(a => !string.IsNullOrEmpty(a.Name)))
            .Select(x => $"{x.Controlador.Name}.{x.Acao.Name}({x.Parametro.Name})")
            .ToList();

        Assert.True(semNome.Count == 0,
            "Parâmetro de action que é coleção ou dicionário precisa de [FromForm(Name = ...)], [FromQuery(Name = ...)] ou "
            + "[Bind(Prefix = ...)]: " + string.Join(", ", semNome));
    }

    /// <summary>
    /// X01 no EF Core 10: toda entidade de escolinha (e a própria Escolinha) tem exatamente UM filtro de consulta, e ele é
    /// anônimo. Com filtro nomeado, o <c>IgnoreQueryFilters()</c> sem argumento deixaria de ser "tudo ou nada" para quem
    /// lê o código, e um segundo filtro poderia ficar de fora da checagem de escolinha.
    /// Atenção: <c>GetDeclaredQueryFilters()</c> e <c>IQueryFilter.Key</c> (nulo = anônimo) são a API do EF Core 10 como
    /// o plano a descreve; confirmar os nomes exatos no primeiro build com o SDK 10.
    /// </summary>
    [Fact]
    public void Toda_entidade_de_escolinha_tem_exatamente_um_filtro_de_consulta_anonimo()
    {
        using var contexto = MinhaEscolinha.Testes.Infraestrutura.BancoDeTestes.NovoContextoSemEscolinha();

        var problemas = new List<string>();
        foreach (var tipo in contexto.Model.GetEntityTypes()
                     .Where(t => typeof(MinhaEscolinha.Domain.Comum.IEntidadeDaEscolinha).IsAssignableFrom(t.ClrType)
                                 || t.ClrType == typeof(MinhaEscolinha.Domain.Configuracao.Escolinha)))
        {
            var filtros = tipo.GetDeclaredQueryFilters().ToList();
            if (filtros.Count != 1)
                problemas.Add($"{tipo.ClrType.Name}: {filtros.Count} filtros (esperado 1)");
            else if (filtros[0].Key is not null)
                problemas.Add($"{tipo.ClrType.Name}: filtro nomeado \"{filtros[0].Key}\" (esperado anônimo)");
        }

        Assert.True(problemas.Count == 0,
            "Entidade de escolinha tem um único filtro de consulta, anônimo (X01): " + string.Join("; ", problemas));
    }

    [Fact]
    public void Assemblies_levam_o_prefixo_do_produto()
    {
        Assert.Equal("MinhaEscolinha.Testes", Assembly.GetExecutingAssembly().GetName().Name);
        Assert.Equal("MinhaEscolinha.Web", typeof(PermissaoAttribute).Assembly.GetName().Name);
    }

    private static IEnumerable<(Type Controlador, MethodInfo Acao)> AcoesDosControllers() =>
        typeof(PermissaoAttribute).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null)
                .Select(m => (Controlador: c, Acao: m)));

    private static IEnumerable<(string Arquivo, string Linha, int Numero)> LinhasDoCodigo()
    {
        var src = Path.Combine(RaizDaSolucao(), "src");
        return Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
            .Where(a => a.EndsWith(".cs") || a.EndsWith(".cshtml"))
            .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !a.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !a.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
            .SelectMany(a => File.ReadLines(a).Select((linha, i) => (Arquivo: Path.GetRelativePath(src, a), Linha: linha, Numero: i + 1)))
            .Where(l => !l.Linha.TrimStart().StartsWith("//"));
    }

    internal static string RaizDaSolucao()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MinhaEscolinha.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Raiz da solução não encontrada.");
    }
}
