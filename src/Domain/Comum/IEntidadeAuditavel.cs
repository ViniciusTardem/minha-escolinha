namespace MinhaEscolinha.Domain.Comum;

/// <summary>
/// Entidade com carimbo de "quem e quando" preenchido pelo <c>AuditoriaInterceptor</c>
/// ao salvar — nenhum serviço atribui estes campos à mão.
/// "Usuário" aqui é a <c>Pessoa</c> dona da credencial que fez a ação; fica nulo quando a gravação
/// não vem de uma pessoa (seed, job).
/// </summary>
public interface IEntidadeAuditavel
{
    DateTime DataCadastro { get; set; }
    Guid? UsuarioCadastro_ID { get; set; }
    DateTime? DataAtualizacao { get; set; }
    Guid? UsuarioAtualizacao_ID { get; set; }
}
