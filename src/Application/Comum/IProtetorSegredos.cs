namespace MinhaEscolinha.Application.Comum;

/// <summary>
/// Cifra segredos de terceiros guardados no banco (a chave de API da conta Asaas de cada escolinha).
/// Implementado com ASP.NET Data Protection: as chaves ficam no banco e, em produção, cifradas por certificado.
/// </summary>
public interface IProtetorSegredos
{
    string Proteger(string segredo);

    /// <summary><c>null</c> quando o texto não pode ser decifrado (chave de proteção perdida ou texto adulterado).</summary>
    string? Desproteger(string protegido);
}

/// <summary>Ambiente em que o app está rodando (para as travas: conta Asaas de produção só em Production).</summary>
public interface IAmbienteExecucao
{
    string Nome { get; }
    bool EhProducao { get; }
}
