using MinhaEscolinha.Application.Eventos;

namespace MinhaEscolinha.Testes.Infraestrutura;

/// <summary>O tempo real em memória: o teste vê quais eventos foram avisados às telas abertas depois de cada gravação.</summary>
public sealed class NotificadorConvocacoesDeTeste : INotificadorConvocacoes
{
    private readonly List<(Guid Escolinha, Guid Evento)> _avisos = [];

    public IReadOnlyList<(Guid Escolinha, Guid Evento)> Avisos
    {
        get { lock (_avisos) return _avisos.ToList(); }
    }

    public void EventosAlterados(IReadOnlyCollection<(Guid Escolinha, Guid Evento)> eventos)
    {
        lock (_avisos) _avisos.AddRange(eventos);
    }

    public void Limpar()
    {
        lock (_avisos) _avisos.Clear();
    }
}
