namespace MinhaEscolinha.Application.Comum;

/// <summary>
/// Relógio do sistema em horário de Brasília. Implementado sobre <see cref="TimeProvider"/>,
/// o que permite congelar/avançar o tempo nos testes (<c>FakeTimeProvider</c>).
/// </summary>
public interface IRelogioBrasil
{
    /// <summary>Data e hora atuais em Brasília (<see cref="DateTimeKind.Unspecified"/>, como gravado no banco).</summary>
    DateTime Agora();

    /// <summary>Data atual em Brasília (00:00).</summary>
    DateTime Hoje();
}
