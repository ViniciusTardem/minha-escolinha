using MinhaEscolinha.Domain.Configuracao.Parametros;

namespace MinhaEscolinha.Domain.Mensageria;

/// <summary>Regras puras do envio (spec §8.2), usadas pelo motor na hora de mandar.</summary>
public static class PoliticaEnvio
{
    /// <summary>
    /// Janela de silêncio (parâmetro <c>mensagem.silencio.janela</c>, padrão 21:00–07:00): dentro dela, devolve quando a
    /// mensagem pode sair (o fim da janela); fora, <c>null</c> (sai agora). A urgente (compromisso nas próximas 24 horas)
    /// não passa por aqui.
    /// </summary>
    public static DateTime? FimDaJanelaDeSilencio(JanelaHorario janela, DateTime agora)
    {
        var hora = TimeOnly.FromDateTime(agora);
        if (!janela.Contem(hora)) return null;

        var fimHoje = DateOnly.FromDateTime(agora).ToDateTime(janela.Fim);
        // Janela que vira a meia-noite: depois do início (ex.: 22:00), o fim (07:00) é no dia seguinte.
        return janela.ViraMeiaNoite && hora >= janela.Inicio ? fimHoje.AddDays(1) : fimHoje;
    }
}
