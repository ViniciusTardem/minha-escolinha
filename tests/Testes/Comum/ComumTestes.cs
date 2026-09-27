using Microsoft.Extensions.Time.Testing;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Infrastructure.Seguranca;
using MinhaEscolinha.Infrastructure.Tempo;

namespace MinhaEscolinha.Testes.Comum;

public class DataHoraBrasilTestes
{
    [Fact]
    public void Agora_converte_o_UTC_do_relogio_para_Brasilia()
    {
        var relogio = new FakeTimeProvider(new DateTimeOffset(2026, 9, 24, 2, 30, 0, TimeSpan.Zero));

        // 02:30 UTC do dia 24 = 23:30 do dia 23 em Brasília (UTC-3): a data de negócio é a de Brasília.
        Assert.Equal(new DateTime(2026, 9, 23, 23, 30, 0), DataHoraBrasil.Agora(relogio));
    }

    [Fact]
    public void RelogioBrasil_Hoje_usa_a_data_de_Brasilia_e_nao_a_do_servidor()
    {
        var relogio = new RelogioBrasil(new FakeTimeProvider(new DateTimeOffset(2026, 9, 24, 1, 0, 0, TimeSpan.Zero)));

        Assert.Equal(new DateTime(2026, 9, 23), relogio.Hoje());
    }

    [Fact]
    public void ResolverFuso_cai_para_UTC_menos_3_quando_nenhum_id_existe()
    {
        var fuso = DataHoraBrasil.ResolverFuso(_ => throw new TimeZoneNotFoundException());

        Assert.Equal(TimeSpan.FromHours(-3), fuso.BaseUtcOffset);
        Assert.False(fuso.SupportsDaylightSavingTime);
    }
}

public class DocumentosTestes
{
    [Theory]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("52998224725", "52998224725")]
    [InlineData("111.111.111-11", null)]
    [InlineData("529.982.247-24", null)]
    [InlineData("", null)]
    public void Cpf_normaliza_e_valida_o_digito(string entrada, string? esperado) =>
        Assert.Equal(esperado, Cpf.Normalizar(entrada));

    [Theory]
    [InlineData("11.222.333/0001-81", "11222333000181")]
    [InlineData("11222333000181", "11222333000181")]
    [InlineData("11.222.333/0001-80", null)]
    [InlineData("00.000.000/0000-00", null)]
    public void Cnpj_normaliza_e_valida_os_digitos(string entrada, string? esperado) =>
        Assert.Equal(esperado, Cnpj.Normalizar(entrada));

    [Theory]
    [InlineData("(43) 99988-7766", "+5543999887766")]
    [InlineData("+55 43 99988-7766", "+5543999887766")]
    [InlineData("043999887766", "+5543999887766")]
    [InlineData("(43) 3333-4444", "+554333334444")]
    [InlineData("(43) 89988-7766", null)] // celular sem o 9 na frente
    [InlineData("9988-7766", null)] // sem DDD
    public void Telefone_vira_E164(string entrada, string? esperado) =>
        Assert.Equal(esperado, Telefone.ParaE164(entrada));

    [Fact]
    public void NormalizarParaBusca_tira_acento_caixa_e_espacos()
    {
        Assert.Equal("joao da silva", TextoHelper.NormalizarParaBusca("  João   da SILVA "));
    }
}

public class GuardaAmbienteBancoTestes
{
    private static readonly BancoProducao[] Producao = [new() { Servidor = "sql.producao.local", Banco = "escolinhaesporte" }];

    private const string ConexaoProducao = "Server=sql.producao.local;Database=escolinhaesporte;User Id=x;Password=y";
    private const string ConexaoLocal = @"Server=VINICIUSTARDEM\SQLEXPRESS;Database=escolinhaesporte;User Id=x;Password=y";

    [Theory]
    [InlineData("Development")]
    [InlineData("Homologacao")]
    public void Fora_de_producao_recusa_o_banco_de_producao(string ambiente)
    {
        var erro = Assert.Throws<InvalidOperationException>(() => GuardaAmbienteBanco.Verificar(ambiente, ConexaoProducao, Producao));

        Assert.Contains("PRODUÇÃO", erro.Message);
        Assert.DoesNotContain("Password", erro.Message); // a mensagem nunca expõe a connection string
    }

    [Fact]
    public void Em_producao_o_banco_de_producao_e_permitido() =>
        GuardaAmbienteBanco.Verificar("Production", ConexaoProducao, Producao);

    [Fact]
    public void Mesmo_nome_de_banco_em_outro_servidor_e_permitido() =>
        GuardaAmbienteBanco.Verificar("Development", ConexaoLocal, Producao);

    [Fact]
    public void Prefixo_tcp_e_porta_padrao_nao_escondem_o_servidor_de_producao()
    {
        const string conexao = "Server=tcp:sql.producao.local,1433;Database=ESCOLINHAESPORTE;User Id=x;Password=y";

        Assert.Throws<InvalidOperationException>(() => GuardaAmbienteBanco.Verificar("Development", conexao, Producao));
    }
}
