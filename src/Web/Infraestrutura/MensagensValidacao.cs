using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace MinhaEscolinha.Web.Infraestrutura;

/// <summary>
/// Mensagens do model binding em pt-BR. Só aparecem quando o valor nem chega a ser convertido (texto num campo
/// numérico, por exemplo): a validação de regra é dos validadores da Application, que já falam português.
/// </summary>
public static class MensagensValidacao
{
    public static void EmPortugues(this DefaultModelBindingMessageProvider mensagens)
    {
        mensagens.SetMissingBindRequiredValueAccessor(campo => $"Informe {campo}.");
        mensagens.SetMissingKeyOrValueAccessor(() => "Informe o valor.");
        mensagens.SetMissingRequestBodyRequiredValueAccessor(() => "O envio chegou vazio.");
        mensagens.SetValueMustNotBeNullAccessor(valor => $"O valor '{valor}' não é válido.");
        mensagens.SetAttemptedValueIsInvalidAccessor((valor, campo) => $"O valor '{valor}' não é válido para {campo}.");
        mensagens.SetNonPropertyAttemptedValueIsInvalidAccessor(valor => $"O valor '{valor}' não é válido.");
        mensagens.SetUnknownValueIsInvalidAccessor(campo => $"O valor informado em {campo} não é válido.");
        mensagens.SetNonPropertyUnknownValueIsInvalidAccessor(() => "O valor informado não é válido.");
        mensagens.SetValueIsInvalidAccessor(valor => $"O valor '{valor}' não é válido.");
        mensagens.SetValueMustBeANumberAccessor(campo => $"{campo} precisa ser um número.");
        mensagens.SetNonPropertyValueMustBeANumberAccessor(() => "O valor precisa ser um número.");
    }
}
