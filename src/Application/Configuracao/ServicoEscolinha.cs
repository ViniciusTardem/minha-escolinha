using FluentValidation;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Configuracao;

namespace MinhaEscolinha.Application.Configuracao;

/// <summary>Dados institucionais da escolinha do contexto (BL-M02-01). Cada campo alterado vai para a trilha de auditoria.</summary>
public class ServicoEscolinha(
    IConfiguracaoRepositorio repositorio,
    RegistroDeAlteracoes registro,
    IValidator<DadosEscolinhaRequisicao> validador)
{
    public async Task<DadosEscolinhaResposta?> ObterAsync(CancellationToken ct = default)
    {
        var e = await repositorio.ObterEscolinhaAsync(ct);
        return e is null ? null : new DadosEscolinhaResposta(e.Escolinha_ID, e.Nome, e.RazaoSocial, e.Cnpj, e.Telefone, e.Email, e.Endereco, e.DataCadastro);
    }

    public async Task<ResultadoOperacao> EditarAsync(DadosEscolinhaRequisicao requisicao, CancellationToken ct = default)
    {
        var validacao = await validador.ValidateAsync(requisicao, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);

        var escolinha = await repositorio.ObterEscolinhaAsync(ct);
        if (escolinha is null) return ResultadoOperacao.Erro("Escolinha não encontrada.");

        var antes = Fotografia(escolinha);
        Aplicar(escolinha, requisicao);
        var depois = Fotografia(escolinha);

        var mudou = false;
        foreach (var (campo, valorAntes) in antes)
            mudou |= registro.SeMudou(nameof(Escolinha), escolinha.Escolinha_ID, campo, valorAntes, depois[campo]);

        if (!mudou) return ResultadoOperacao.Ok("Nada mudou.");
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok("Dados da escolinha atualizados.");
    }

    /// <summary>Copia os dados da requisição para a entidade, normalizando CNPJ, telefone, UF e CEP.</summary>
    internal static void Aplicar(Escolinha e, DadosEscolinhaRequisicao r)
    {
        e.Nome = r.Nome.Trim();
        e.RazaoSocial = Vazio(r.RazaoSocial);
        e.Cnpj = Domain.Comum.Cnpj.Normalizar(r.Cnpj);
        e.Telefone = Domain.Comum.Telefone.ParaE164(r.Telefone);
        e.Email = Vazio(r.Email)?.ToLowerInvariant();
        e.Endereco ??= new Endereco();
        e.Endereco.Logradouro = Vazio(r.Logradouro);
        e.Endereco.Numero = Vazio(r.Numero);
        e.Endereco.Complemento = Vazio(r.Complemento);
        e.Endereco.Bairro = Vazio(r.Bairro);
        e.Endereco.Cidade = Vazio(r.Cidade);
        e.Endereco.Uf = Vazio(r.Uf)?.ToUpperInvariant();
        e.Endereco.Cep = TextoHelper.SomenteDigitos(r.Cep) is { Length: 8 } cep ? cep : null;
    }

    private static Dictionary<string, string?> Fotografia(Escolinha e) => new()
    {
        ["Nome"] = e.Nome,
        ["RazaoSocial"] = e.RazaoSocial,
        ["Cnpj"] = e.Cnpj,
        ["Telefone"] = e.Telefone,
        ["Email"] = e.Email,
        ["Endereco"] = e.Endereco?.Resumo(),
        ["Cep"] = e.Endereco?.Cep
    };

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
