using MinhaEscolinha.Application.Comum;

namespace MinhaEscolinha.Testes.Infraestrutura;

/// <summary>O e-mail do teste: guarda o que seria enviado. <see cref="FalharProximo"/> simula o SMTP fora do ar.</summary>
public sealed class EnvioEmailFalso : IEnvioEmail
{
    public List<(string Para, string Assunto, string Corpo)> Enviados { get; } = [];
    public bool FalharProximo { get; set; }
    public string Descricao => "e-mail de teste";

    public Task EnviarAsync(string para, string assunto, string corpoHtml, CancellationToken ct = default)
    {
        if (FalharProximo)
        {
            FalharProximo = false;
            throw new InvalidOperationException("SMTP fora do ar (teste).");
        }
        Enviados.Add((para, assunto, corpoHtml));
        return Task.CompletedTask;
    }
}

public sealed class AgendadorEmailDeTeste : IAgendadorEmail
{
    public List<Guid> Recuperacoes { get; } = [];
    public void EnviarRecuperacaoSenha(Guid recuperacaoId) => Recuperacoes.Add(recuperacaoId);
}
