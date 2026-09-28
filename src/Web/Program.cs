using System.Globalization;
using System.Threading.RateLimiting;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinhaEscolinha.Application;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Integracao.Stripe;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Infrastructure;
using MinhaEscolinha.Infrastructure.Cadastro;
using MinhaEscolinha.Infrastructure.Data.Contexto;
using MinhaEscolinha.Infrastructure.Data.Seed;
using MinhaEscolinha.Infrastructure.Financeiro;
using MinhaEscolinha.Infrastructure.Mensageria;
using MinhaEscolinha.Infrastructure.Plataforma;
using MinhaEscolinha.Infrastructure.Seguranca;
using MinhaEscolinha.Web.Filters;
using MinhaEscolinha.Web.Infraestrutura;
using MinhaEscolinha.Web.Middleware;
using Serilog;

// Logger bootstrap: grava em arquivo desde antes do builder para capturar erro de startup
// (na hospedagem compartilhada não há console para olhar).
var pastaLogs = Path.Combine(AppContext.BaseDirectory, "logs");
try { Directory.CreateDirectory(pastaLogs); } catch { /* sem permissão: segue só com console */ }

const string ModeloLog = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(pastaLogs, "startup-.log"), rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14, outputTemplate: ModeloLog)
    .CreateBootstrapLogger();

try
{
    Log.Information("Iniciando MinhaEscolinha.Web — ambiente {Ambiente}, base {Base}",
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "(não definido)", AppContext.BaseDirectory);

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((contexto, servicos, configuracao) => configuracao
        .ReadFrom.Configuration(contexto.Configuration)
        .ReadFrom.Services(servicos)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(Path.Combine(pastaLogs, "app-.log"), rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30, outputTemplate: ModeloLog));

    // ── Trava de ambiente: antes de qualquer serviço tocar o banco ──
    var guarda = builder.Configuration.GetSection(GuardaAmbienteOpcoes.Secao).Get<GuardaAmbienteOpcoes>() ?? new();
    GuardaAmbienteBanco.Verificar(builder.Environment.EnvironmentName,
        builder.Configuration.GetConnectionString(MinhaEscolinha.Infrastructure.DependencyInjection.ConexaoPadrao), guarda.BancosProducao);
    if (guarda.BancosProducao.Count == 0)
        Log.Warning("GuardaAmbiente:BancosProducao vazio — a trava de ambiente não protege nada até ser configurada.");

    // ── Serviços ──
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IUsuarioAtual, UsuarioAtualHttp>();
    builder.Services.AddSingleton<IAmbienteExecucao, AmbienteExecucaoWeb>();
    builder.Services.AddAplicacao();
    builder.Services.AddInfraestrutura(builder.Configuration, builder.Environment.EnvironmentName, aviso => Log.Warning(aviso));

    // Chaves no banco: o cookie de login sobrevive à reciclagem do app pool (e a chave Asaas cifrada continua legível).
    var dataProtection = builder.Services.AddDataProtection()
        .SetApplicationName("MinhaEscolinha")
        .PersistKeysToDbContext<EscolinhaDbContext>();
    // Produção: as chaves ficam cifradas com um certificado (.pfx fora do site; senha em variável de ambiente).
    if (builder.Configuration["DataProtection:CertificadoArquivo"] is { Length: > 0 } certificado)
    {
        dataProtection.ProtectKeysWithCertificate(System.Security.Cryptography.X509Certificates.X509CertificateLoader
            .LoadPkcs12FromFile(certificado, builder.Configuration["DataProtection:CertificadoSenha"]));
        Log.Information("Chaves de Data Protection protegidas pelo certificado {Arquivo}.", Path.GetFileName(certificado));
    }
    else if (builder.Environment.IsProduction())
    {
        // Sem certificado, quem copiar o banco decifra a chave Asaas das escolinhas: produção não sobe assim.
        throw new InvalidOperationException(
            "DataProtection:CertificadoArquivo é obrigatório em Production (as chaves de API das escolinhas ficam cifradas por ele).");
    }
    else if (!builder.Environment.IsDevelopment())
    {
        Log.Warning("DataProtection:CertificadoArquivo não configurado — as chaves ficam no banco sem cifragem.");
    }

    builder.Services.AddScoped<EventosCookieEscolinha>();
    // Massa de demonstração (/dev/massa, só em Development).
    builder.Services.AddScoped<MinhaEscolinha.Web.Infraestrutura.Desenvolvimento.MassaDemonstracao>();
    builder.Services.AddAuthentication(ClaimsEscolinha.Esquema)
        .AddCookie(ClaimsEscolinha.Esquema, opcoes =>
        {
            opcoes.Cookie.Name = ClaimsEscolinha.NomeCookie;
            opcoes.Cookie.HttpOnly = true;
            opcoes.Cookie.SameSite = SameSiteMode.Lax;
            opcoes.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            opcoes.ExpireTimeSpan = TimeSpan.FromHours(10);
            opcoes.SlidingExpiration = true;
            opcoes.LoginPath = "/Conta/Login";
            opcoes.LogoutPath = "/Conta/Sair";
            opcoes.AccessDeniedPath = "/Conta/AcessoNegado";
            opcoes.EventsType = typeof(EventosCookieEscolinha);
        });

    builder.Services.AddAuthorizationBuilder()
        .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
        .AddPolicy(Politicas.Plataforma, p => p.RequireClaim(ClaimsEscolinha.AdministradorPlataforma, "true"))
        // /jobs fica fora do MVC (e do filtro de troca de senha): a pendência de troca bloqueia aqui.
        .AddPolicy(Politicas.Jobs, p => p.RequireClaim(ClaimsEscolinha.AdministradorPlataforma, "true")
            .RequireAssertion(c => !c.User.PrecisaTrocarSenha()));

    builder.Services.AddAntiforgery(opcoes =>
    {
        opcoes.HeaderName = "RequestVerificationToken";
        opcoes.Cookie.Name = ".MinhaEscolinha.Antiforgery";
        opcoes.Cookie.HttpOnly = true;
        opcoes.Cookie.SameSite = SameSiteMode.Strict;
        opcoes.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

    builder.Services.AddControllersWithViews(opcoes =>
    {
        opcoes.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        opcoes.Filters.Add<ExigirTrocaSenhaFilter>();
        // Quem valida é o validador da Application (em português). Sem isto, string não anulável ganha um
        // [Required] implícito e a tela mostra "The X field is required." junto com a mensagem do validador.
        opcoes.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        opcoes.ModelBindingMessageProvider.EmPortugues();
        // Valor em reais como o gestor digita ("1.500,00" e também "150.50"), D30.
        opcoes.ModelBinderProviders.Insert(0, new DecimalBrasileiroModelBinderProvider());
    });

    builder.Services.AddSignalR();

    builder.Services.AddRateLimiter(opcoes =>
    {
        opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        opcoes.AddPolicy(PoliticasLimite.Login, contexto => RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        opcoes.OnRejected = async (contexto, ct) =>
        {
            Log.Warning("Limite de tentativas de login atingido para o IP {Ip}.", contexto.HttpContext.Connection.RemoteIpAddress);
            contexto.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
            await contexto.HttpContext.Response.WriteAsync(
                "Muitas tentativas de login a partir deste endereço. Aguarde um minuto e tente de novo.", ct);
        };
    });

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<EscolinhaDbContext>("banco")
        .AddCheck<HangfireHealthCheck>("hangfire");

    var app = builder.Build();

    // ── Banco: migrations + seed do 1º administrador da plataforma + parâmetros novos nas escolinhas ──
    using (var escopo = app.Services.CreateScope())
    {
        var contexto = escopo.ServiceProvider.GetRequiredService<EscolinhaDbContext>();
        Log.Information("Aplicando migrations do banco da Minha Escolinha...");
        await contexto.Database.MigrateAsync();

        await SeedAdministradorPlataforma.ExecutarAsync(contexto, app.Configuration,
            escopo.ServiceProvider.GetRequiredService<IHashSenha>(),
            escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SeedAdministradorPlataforma)));
        await escopo.ServiceProvider.GetRequiredService<ReconciliadorParametros>().ReconciliarAsync();
        // Catálogo de templates de WhatsApp: grava os textos padrão que faltam (nunca sobrescreve o que a equipe editou)
        var templatesNovos = await escopo.ServiceProvider.GetRequiredService<ServicoTemplates>().ReconciliarAsync();
        if (templatesNovos > 0) Log.Information("{Quantos} template(s) de WhatsApp gravado(s) no catálogo.", templatesNovos);

        // Varredura das compras de créditos pendentes no Stripe (cobre o webhook perdido)
        JobsPlataforma.RegistrarRecorrentes(escopo.ServiceProvider.GetRequiredService<IRecurringJobManager>(),
            escopo.ServiceProvider.GetRequiredService<OpcoesStripe>());
        // Sessões de treino: todo dia, o horizonte até o fim do mês seguinte (BL-M04-04)
        JobsTurmas.RegistrarRecorrentes(escopo.ServiceProvider.GetRequiredService<IRecurringJobManager>());
        // Motor de mensagens: a varredura de 5 em 5 minutos (job perdido, fim da janela de silêncio, retidas com saldo)
        JobsMensageria.RegistrarRecorrentes(escopo.ServiceProvider.GetRequiredService<IRecurringJobManager>());
        // Mensalidades: a rotina diária (projeção, régua, emissão) e a varredura de hora em hora na Asaas (S4)
        JobsFinanceiro.RegistrarRecorrentes(escopo.ServiceProvider.GetRequiredService<IRecurringJobManager>());
    }

    // ── Pipeline ──
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/Erro");
        app.UseHsts();
    }
    // A API responde JSON: a página de erro HTML fica só para as telas.
    app.UseWhen(c => !c.Request.Path.StartsWithSegments("/api"), ramo => ramo.UseStatusCodePagesWithReExecute("/Erro/{0}"));

    app.UseHttpsRedirection();
    app.UseMiddleware<CabecalhosSegurancaMiddleware>();

    // Antes da autenticação: CSS/JS/fontes servidos sem passar pela FallbackPolicy.
    app.UseStaticFiles();

    var ptBr = new CultureInfo("pt-BR");
    app.UseRequestLocalization(new RequestLocalizationOptions
    {
        DefaultRequestCulture = new RequestCulture(ptBr, ptBr),
        SupportedCultures = [ptBr],
        SupportedUICultures = [ptBr]
    });

    app.UseRouting();
    app.UseRateLimiter();
    app.UseAuthentication();
    // Depois da autenticação: escolinha do cookie + papéis relidos do banco a cada requisição.
    app.UseMiddleware<ContextoEscolinhaMiddleware>();
    app.UseAuthorization();
    app.UseMiddleware<ExceptionLoggingMiddleware>();

    app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = Saude.EscreverRespostaAsync })
        .AllowAnonymous();

    app.MapHangfireDashboardWithAuthorizationPolicy(Politicas.Jobs, "/jobs", new DashboardOptions
    {
        DashboardTitle = "Jobs — Minha Escolinha",
        AppPath = "/",
        DisplayStorageConnectionString = false
    });

    // Páginas explicativas dos módulos das próximas fatias (/turmas, /alunos...): saem do catálogo; somem quando o módulo ficar pronto.
    app.MapControllerRoute(
        name: "modulo",
        pattern: "{chave}",
        defaults: new { controller = "Modulos", action = "Index" },
        constraints: new { chave = new Microsoft.AspNetCore.Routing.Constraints.RegexRouteConstraint(
            $"^({string.Join('|', CatalogoModulos.ChavesPaginaExplicativa)})$") });

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Painel}/{action=Index}/{id?}");

    Log.Information("Aplicação pronta.");
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Falha fatal no startup: {Mensagem}", ex.Message);
    try
    {
        File.WriteAllText(Path.Combine(pastaLogs, $"crash-{DataHoraBrasil.Agora():yyyyMMdd-HHmmss}.log"),
            $"Data (Brasília): {DataHoraBrasil.Agora():yyyy-MM-dd HH:mm:ss}\n" +
            $"BaseDirectory: {AppContext.BaseDirectory}\n" +
            $"Ambiente: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}\n\n{ex}\n");
    }
    catch { /* sem onde gravar */ }
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public static class PoliticasLimite
{
    public const string Login = "login";
}
