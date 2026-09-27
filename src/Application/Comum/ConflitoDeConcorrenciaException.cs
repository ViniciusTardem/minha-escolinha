namespace MinhaEscolinha.Application.Comum;

/// <summary>
/// Outra operação gravou o mesmo registro antes (versão de linha): a gravação desta foi desfeita inteira. Quem chama
/// relê num escopo novo e decide de novo. O repositório traduz o erro do banco para esta exceção, e a Application não
/// depende do EF.
/// </summary>
public class ConflitoDeConcorrenciaException(string mensagem, Exception? interna = null) : Exception(mensagem, interna);
