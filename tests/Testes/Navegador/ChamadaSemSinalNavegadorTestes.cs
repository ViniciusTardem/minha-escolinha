using Microsoft.Playwright;

namespace MinhaEscolinha.Testes.Navegador;

/// <summary>
/// Só roda com o app no ar e as variáveis <c>ESC_E2E_*</c> (D22): o endereço, o login e a senha de um professor ou da
/// gestão, e a sessão de treino em que a chamada está aberta. Sem elas, o teste é ignorado (a suíte normal não depende
/// de navegador nem de app rodando).
/// </summary>
public sealed class FatoComNavegadorAttribute : FactAttribute
{
    public static readonly string? Url = Environment.GetEnvironmentVariable("ESC_E2E_URL");
    public static readonly string? Login = Environment.GetEnvironmentVariable("ESC_E2E_LOGIN");
    public static readonly string? Senha = Environment.GetEnvironmentVariable("ESC_E2E_SENHA");
    public static readonly string? Sessao = Environment.GetEnvironmentVariable("ESC_E2E_SESSAO");

    public FatoComNavegadorAttribute()
    {
        if (string.IsNullOrWhiteSpace(Url) || string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Senha) || string.IsNullOrWhiteSpace(Sessao))
            Skip = "Teste de navegador: defina ESC_E2E_URL, ESC_E2E_LOGIN, ESC_E2E_SENHA e ESC_E2E_SESSAO, com o app rodando.";
    }
}

/// <summary>
/// CA-21 no navegador de verdade (Chrome instalado, com o certificado de desenvolvimento aceito só nesta instância):
/// a chamada abre, o aparelho perde o sinal, a marcação fica na fila do aparelho, a página reabre sem sinal pelo service
/// worker com a marcação guardada, e ao voltar o sinal a fila chega ao servidor uma vez só. No fim, a marcação volta a
/// ser a de antes (o teste não deixa rastro nos dados de validação, só no registro de alterações).
/// </summary>
public sealed class ChamadaSemSinalNavegadorTestes
{
    private const int Prazo = 20_000;

    [FatoComNavegador]
    public async Task A_chamada_marcada_sem_sinal_reabre_pelo_aparelho_e_chega_ao_servidor_quando_o_sinal_volta()
    {
        var url = FatoComNavegadorAttribute.Url!.TrimEnd('/');
        using var playwright = await Playwright.CreateAsync();
        await using var chrome = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = "chrome", Headless = true, Args = ["--ignore-certificate-errors"]
        });
        await using var contexto = await chrome.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true, ViewportSize = new ViewportSize { Width = 375, Height = 812 }, ServiceWorkers = ServiceWorkerPolicy.Allow
        });
        var pagina = await contexto.NewPageAsync();
        pagina.SetDefaultTimeout(Prazo);

        // Entra e abre a lista da chamada, que registra o service worker e guarda as sessões do dia no aparelho.
        await pagina.GotoAsync($"{url}/Conta/Login");
        await pagina.FillAsync("#Login", FatoComNavegadorAttribute.Login!);
        await pagina.FillAsync("#Senha", FatoComNavegadorAttribute.Senha!);
        await pagina.ClickAsync("button[type=submit]");
        await pagina.WaitForURLAsync(u => !u.Contains("/Conta/Login"));

        var sessao = $"{url}/Chamada/Sessao/{FatoComNavegadorAttribute.Sessao}";
        await pagina.GotoAsync(sessao);
        await pagina.EvaluateAsync("() => navigator.serviceWorker.ready");
        await pagina.ReloadAsync(); // agora pelo service worker: a página fica guardada no aparelho
        var linha = pagina.Locator("[data-aluno]").First;
        var aluno = await linha.GetAttributeAsync("data-aluno");
        var antes = await linha.GetAttributeAsync("data-situacao") ?? "";
        var nova = antes == "Falta" ? "Presente" : "Falta";

        // Sem sinal: a marcação fica na fila do aparelho.
        await contexto.SetOfflineAsync(true);
        await linha.Locator($"[data-esc-marcar={nova}]").ClickAsync();
        await EsperarAsync(pagina, "() => Number(document.querySelector('[data-esc-conexao]').dataset.pendentes) >= 1");

        // A página reabre sem sinal (service worker) e mostra a marcação da fila.
        await pagina.ReloadAsync();
        var linhaSemSinal = pagina.Locator($"[data-aluno='{aluno}']");
        await EsperarAsync(pagina, $"() => document.querySelector(\"[data-aluno='{aluno}']\").dataset.situacao === '{nova}'");
        Assert.Equal(nova, await linhaSemSinal.GetAttributeAsync("data-situacao"));

        // O sinal volta: a fila chega ao servidor e esvazia.
        await contexto.SetOfflineAsync(false);
        await pagina.EvaluateAsync("() => window.dispatchEvent(new Event('online'))");
        await EsperarAsync(pagina, "() => document.querySelector('[data-esc-conexao]').dataset.pendentes === '0'");

        // O servidor tem a marcação: outra página, sem a fila do aparelho, mostra a mesma situação.
        var conferencia = await contexto.NewPageAsync();
        await conferencia.GotoAsync(sessao);
        Assert.Equal(nova, await conferencia.Locator($"[data-aluno='{aluno}']").GetAttributeAsync("data-situacao"));

        // Volta ao que era (quando havia marcação antes).
        if (antes.Length > 0)
        {
            await conferencia.Locator($"[data-aluno='{aluno}'] [data-esc-marcar={antes}]").ClickAsync();
            await EsperarAsync(conferencia, "() => document.querySelector('[data-esc-conexao]').dataset.pendentes === '0'");
        }
    }

    private static Task EsperarAsync(IPage pagina, string condicao) =>
        pagina.WaitForFunctionAsync(condicao, null, new PageWaitForFunctionOptions { Timeout = Prazo });
}
