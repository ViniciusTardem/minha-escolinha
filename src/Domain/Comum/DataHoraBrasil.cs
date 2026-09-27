namespace MinhaEscolinha.Domain.Comum;

/// <summary>
/// Horário de Brasília. O servidor de produção pode estar em outro fuso: toda data de negócio
/// (vencimento, sessão de treino, janela de silêncio, lembrete de convocação) passa por aqui.
/// Regra de ouro: nunca <c>DateTime.Now</c>/<c>Today</c>/<c>UtcNow</c> fora deste arquivo —
/// no código de aplicação use <c>IRelogioBrasil</c> (testável com <c>FakeTimeProvider</c>).
/// Portado do AlternativaDepartamentoPessoal sem alteração de comportamento.
/// </summary>
public static class DataHoraBrasil
{
    /// <summary>Id Windows e id IANA (Linux/containers e Windows com ICU) do mesmo fuso.</summary>
    public const string IdWindows = "E. South America Standard Time";
    public const string IdIana = "America/Sao_Paulo";

    private static readonly TimeZoneInfo _tzBrasilia = ResolverFuso();

    public static TimeZoneInfo TimeZone => _tzBrasilia;

    public static DateTime Agora() => Agora(TimeProvider.System);

    public static DateTime Agora(TimeProvider relogio) =>
        TimeZoneInfo.ConvertTimeFromUtc(relogio.GetUtcNow().UtcDateTime, _tzBrasilia);

    public static DateTime ConverterParaBrasilia(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _tzBrasilia);

    /// <summary>
    /// Converte um horário de Brasília para UTC. Horário inexistente (salto de horário de verão,
    /// ex.: 00:30 de um domingo de início do verão até 2018) seria exceção no
    /// <see cref="TimeZoneInfo.ConvertTimeToUtc(DateTime, TimeZoneInfo)"/>: é empurrado para
    /// depois do salto, que é o instante válido seguinte.
    /// </summary>
    public static DateTime ConverterParaUtc(DateTime localBrasilia)
    {
        var local = DateTime.SpecifyKind(localBrasilia, DateTimeKind.Unspecified);

        if (_tzBrasilia.IsInvalidTime(local))
        {
            var ajuste = _tzBrasilia.GetAdjustmentRules()
                .FirstOrDefault(r => r.DateStart <= local.Date && r.DateEnd >= local.Date);
            local = local.Add(ajuste?.DaylightDelta ?? TimeSpan.FromHours(1));
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, _tzBrasilia);
    }

    internal static TimeZoneInfo ResolverFuso(Func<string, TimeZoneInfo>? buscar = null)
    {
        buscar ??= TimeZoneInfo.FindSystemTimeZoneById;

        foreach (var id in new[] { IdWindows, IdIana })
        {
            try { return buscar(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        // Último recurso: Brasília sem horário de verão (abolido em 2019). Melhor que derrubar o app.
        return TimeZoneInfo.CreateCustomTimeZone(IdIana, TimeSpan.FromHours(-3), "Brasília", "Brasília");
    }
}
