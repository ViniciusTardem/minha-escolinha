using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Infrastructure.Tempo;

public class RelogioBrasil(TimeProvider timeProvider) : IRelogioBrasil
{
    public DateTime Agora() => DataHoraBrasil.Agora(timeProvider);

    public DateTime Hoje() => Agora().Date;
}
