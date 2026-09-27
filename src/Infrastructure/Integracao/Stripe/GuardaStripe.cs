using MinhaEscolinha.Application.Integracao.Stripe;

namespace MinhaEscolinha.Infrastructure.Integracao.Stripe;

/// <summary>
/// Trava de ambiente do Stripe, no padrão do AlternativaDepartamentoPessoal: roda no startup e torna o erro impossível.
/// Fora de Production não existe cobrança de verdade; em Production não existe simulador nem modo de teste (crédito
/// comprado com cartão de teste gastaria WhatsApp de verdade). Para testar num servidor, use o ambiente Homologacao.
/// </summary>
public static class GuardaStripe
{
    /// <summary>
    /// Confere as opções. Simulado em Production não derruba o site: a compra de créditos fica desligada (com aviso).
    /// </summary>
    /// <exception cref="InvalidOperationException">Combinação proibida: o site não sobe.</exception>
    public static void Validar(string ambiente, OpcoesStripe opcoes, Action<string>? aviso = null)
    {
        if (!opcoes.Habilitado) return;
        var producao = string.Equals(ambiente, "Production", StringComparison.OrdinalIgnoreCase);

        if (!Uri.TryCreate(opcoes.UrlPublica, UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttps && !url.IsLoopback))
            throw new InvalidOperationException($"Stripe:UrlPublica inválida ({opcoes.UrlPublica}): use o endereço https do sistema.");

        if (opcoes.Simulado)
        {
            if (producao)
            {
                opcoes.Habilitado = false;
                aviso?.Invoke("Stripe:Modo = Simulado em Production: a compra de créditos fica desligada até o Stripe de produção ser configurado.");
            }
            return;
        }

        if (opcoes.Teste)
        {
            if (producao)
                throw new InvalidOperationException(
                    "Stripe:Modo = Teste em Production: crédito comprado com cartão de teste gastaria WhatsApp de verdade. " +
                    "Em produção use Producao (ou Stripe:Habilitado = false); para testar num servidor, use o ambiente Homologacao.");
            if (!ChaveDeTeste(opcoes.ChaveSecreta))
                throw new InvalidOperationException(
                    "Stripe:ChaveSecreta precisa ser uma chave de TESTE (sk_test_... ou rk_test_...) no modo Teste (user-secrets).");
            if (string.IsNullOrWhiteSpace(opcoes.SegredoWebhook))
                aviso?.Invoke("Stripe:SegredoWebhook não configurado: os webhooks serão recusados; a varredura e a volta do pagamento conferem as compras.");
            return;
        }

        if (string.Equals(opcoes.Modo, OpcoesStripe.ModoProducao, StringComparison.OrdinalIgnoreCase))
        {
            if (!producao)
                throw new InvalidOperationException(
                    $"Stripe:Modo = Producao no ambiente {ambiente}: fora de Production só Simulado ou Teste (produção cobra de verdade).");
            if (!opcoes.ConfirmarProducao)
                throw new InvalidOperationException("Stripe:ConfirmarProducao precisa ser true para cobrar de verdade.");
            if (!ChaveDeProducao(opcoes.ChaveSecreta))
                throw new InvalidOperationException("Stripe:ChaveSecreta precisa ser a chave de produção (sk_live_... ou rk_live_...).");
            if (string.IsNullOrWhiteSpace(opcoes.SegredoWebhook))
                throw new InvalidOperationException("Stripe:SegredoWebhook é obrigatório em produção (sem ele o webhook não é conferido).");
            return;
        }

        throw new InvalidOperationException($"Stripe:Modo desconhecido: {opcoes.Modo} (use Simulado, Teste ou Producao).");
    }

    public static bool ChaveDeTeste(string? chave) =>
        chave is not null && (chave.StartsWith("sk_test_", StringComparison.Ordinal) || chave.StartsWith("rk_test_", StringComparison.Ordinal));

    public static bool ChaveDeProducao(string? chave) =>
        chave is not null && (chave.StartsWith("sk_live_", StringComparison.Ordinal) || chave.StartsWith("rk_live_", StringComparison.Ordinal));
}
