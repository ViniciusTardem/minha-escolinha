using Microsoft.EntityFrameworkCore;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Domain.Auditoria;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Infrastructure.Data.Contexto;

namespace MinhaEscolinha.Infrastructure.Configuracao;

/// <summary>Configuração da escolinha do contexto — tudo passa pelo filtro global (nada aqui ignora o filtro).</summary>
public class ConfiguracaoRepositorio(EscolinhaDbContext contexto) : IConfiguracaoRepositorio
{
    public Task<Escolinha?> ObterEscolinhaAsync(CancellationToken ct = default) =>
        contexto.Escolinhas.FirstOrDefaultAsync(ct);

    public void AdicionarEscolinha(Escolinha escolinha) => contexto.Escolinhas.Add(escolinha);

    public async Task<IReadOnlyList<Modalidade>> ListarModalidadesAsync(CancellationToken ct = default) =>
        await contexto.Modalidades.OrderBy(m => m.Nome).ToListAsync(ct);

    public Task<Modalidade?> ObterModalidadeAsync(Guid id, CancellationToken ct = default) =>
        contexto.Modalidades.FirstOrDefaultAsync(m => m.Modalidade_ID == id, ct);

    public Task<bool> NomeModalidadeEmUsoAsync(string nome, Guid? excetoId, CancellationToken ct = default) =>
        contexto.Modalidades.AnyAsync(m => m.Nome == nome && (excetoId == null || m.Modalidade_ID != excetoId), ct);

    public void AdicionarModalidade(Modalidade modalidade) => contexto.Modalidades.Add(modalidade);

    public async Task<IReadOnlyList<ParametroEscolinha>> ListarParametrosAsync(CancellationToken ct = default) =>
        await contexto.Parametros.ToListAsync(ct);

    public void AdicionarParametro(ParametroEscolinha parametro) => contexto.Parametros.Add(parametro);

    public Task<ContaAsaasEscolinha?> ObterContaAsaasAsync(CancellationToken ct = default) =>
        contexto.ContasAsaas.FirstOrDefaultAsync(ct);

    public void AdicionarContaAsaas(ContaAsaasEscolinha conta) => contexto.ContasAsaas.Add(conta);

    public Task<ContaCreditos?> ObterContaCreditosAsync(CancellationToken ct = default) =>
        contexto.ContasCreditos.FirstOrDefaultAsync(ct);

    public void AdicionarContaCreditos(ContaCreditos conta) => contexto.ContasCreditos.Add(conta);

    public void AdicionarMovimentoCredito(MovimentoCredito movimento) => contexto.MovimentosCredito.Add(movimento);

    public async Task<Pagina<MovimentoCredito>> ListarMovimentosCreditoAsync(int pagina, int tamanho, CancellationToken ct = default)
    {
        var consulta = contexto.MovimentosCredito.AsNoTracking();
        var total = await consulta.CountAsync(ct);
        var itens = await consulta.OrderByDescending(m => m.Em)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .ToListAsync(ct);
        return new Pagina<MovimentoCredito>(itens, total, pagina, tamanho);
    }

    public void Registrar(RegistroAlteracao registro) => contexto.RegistrosAlteracao.Add(registro);

    public async Task<IReadOnlyList<RegistroAlteracao>> ListarAlteracoesAsync(string entidade, string? campo, int limite, CancellationToken ct = default) =>
        await contexto.RegistrosAlteracao.AsNoTracking()
            .Where(r => r.Entidade == entidade && (campo == null || r.Campo == campo))
            .OrderByDescending(r => r.Em)
            .Take(limite)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RegistroAlteracao>> ListarAlteracoesDoRegistroAsync(string entidade, Guid registroId, int limite,
        CancellationToken ct = default) =>
        await contexto.RegistrosAlteracao.AsNoTracking()
            .Where(r => r.Entidade == entidade && r.Registro_ID == registroId)
            .OrderByDescending(r => r.Em)
            .Take(limite)
            .ToListAsync(ct);

    public Task SalvarAsync(CancellationToken ct = default) => contexto.SaveChangesAsync(ct);
}
