using Microsoft.AspNetCore.Mvc.ModelBinding;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Web.Infraestrutura;

/// <summary>
/// Valor decimal digitado na tela (mensalidade, preço): "150", "150,50", "1.500,00" ou "R$ 1.500,00" (<see cref="Moeda"/>).
/// Sem ele, o binder padrão em pt-BR leria "150.50" como 15.050 — o ponto é separador de milhar na cultura (D30).
/// </summary>
public sealed class DecimalBrasileiroModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext contexto)
    {
        var valor = contexto.ValueProvider.GetValue(contexto.ModelName);
        if (valor == ValueProviderResult.None) return Task.CompletedTask;

        contexto.ModelState.SetModelValue(contexto.ModelName, valor);
        var texto = valor.FirstValue;
        var anulavel = Nullable.GetUnderlyingType(contexto.ModelType) is not null;

        if (string.IsNullOrWhiteSpace(texto))
        {
            if (anulavel) contexto.Result = ModelBindingResult.Success(null);
            else
                contexto.ModelState.TryAddModelError(contexto.ModelName,
                    contexto.ModelMetadata.ModelBindingMessageProvider.ValueMustNotBeNullAccessor(texto ?? string.Empty));
            return Task.CompletedTask;
        }

        if (Moeda.TentarLer(texto, out var numero))
        {
            contexto.Result = ModelBindingResult.Success(numero);
            return Task.CompletedTask;
        }

        contexto.ModelState.TryAddModelError(contexto.ModelName,
            contexto.ModelMetadata.ModelBindingMessageProvider.AttemptedValueIsInvalidAccessor(texto,
                contexto.ModelMetadata.DisplayName ?? contexto.ModelMetadata.Name ?? contexto.ModelName));
        return Task.CompletedTask;
    }
}

public sealed class DecimalBrasileiroModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext contexto) =>
        contexto.Metadata.ModelType == typeof(decimal) || contexto.Metadata.ModelType == typeof(decimal?)
            ? new DecimalBrasileiroModelBinder()
            : null;
}
