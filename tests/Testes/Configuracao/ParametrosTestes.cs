using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Testes.Configuracao;

/// <summary>Catálogo de parâmetros (R7, BL-M02-06 a 09 e 12) e CA-18: nenhuma regra sensível é constante no código.</summary>
public class CatalogoParametrosTestes
{
    /// <summary>A lista literal da regra R7 da spec, cada item com a chave do catálogo que o atende.</summary>
    public static readonly TheoryData<string, string> ItensDaR7 = new()
    {
        { "Limite de faltas", ChavesParametro.PresencaLimiteFaltas },
        { "Janela de risco de evasão", ChavesParametro.PresencaRiscoJanela },
        { "Percentual de risco de evasão", ChavesParametro.PresencaRiscoPercentual },
        { "Tom de mensagem", ChavesParametro.PresencaTomAlerta },
        { "Multa", ChavesParametro.FinanceiroMulta },
        { "Juros", ChavesParametro.FinanceiroJuros },
        { "Dias de atraso para sinalização", ChavesParametro.ConvocacaoDiasAtrasoSinalizacao },
        { "Bloqueio por inadimplência", ChavesParametro.ConvocacaoBloquearInadimplente },
        { "Janela de silêncio", ChavesParametro.MensagemJanelaSilencio },
        { "Mínimo de confirmados", ChavesParametro.TurmaMinimoConfirmados },
        { "Janela de disparo", ChavesParametro.ConvocacaoJanelaDisparo },
        { "Janela de lembrete", ChavesParametro.ConvocacaoLembreteHorasAntes }
    };

    [Theory]
    [MemberData(nameof(ItensDaR7))]
    public void Todo_parametro_citado_na_R7_existe_no_catalogo(string item, string chave) =>
        Assert.True(CatalogoParametros.Obter(chave) is not null, $"R7 \"{item}\" sem parâmetro no catálogo ({chave}).");

    [Fact]
    public void As_chaves_sao_unicas() =>
        Assert.Equal(CatalogoParametros.Todos.Count, CatalogoParametros.Todos.Select(d => d.Chave).Distinct().Count());

    [Fact]
    public void Todo_valor_padrao_passa_na_propria_validacao()
    {
        foreach (var definicao in CatalogoParametros.Todos.Where(d => d.ValorPadrao is not null))
        {
            var resultado = ValidadorParametro.Validar(definicao, definicao.ValorPadrao);
            Assert.True(resultado.Valido, $"{definicao.Chave}: {resultado.Erro}");
            Assert.Equal(definicao.ValorPadrao, resultado.Valor);
        }
    }

    [Fact]
    public void Os_padroes_sugeridos_pela_spec_sao_os_do_catalogo()
    {
        var valores = ValoresParametros.SoPadroes();

        Assert.Equal(15, valores.Presenca.JanelaAntiDuplicidadeDias);
        Assert.Equal(new JanelaHorario(new TimeOnly(21, 0), new TimeOnly(7, 0)), valores.Mensagem.JanelaSilencio);
        Assert.Equal(3, valores.Matricula.TrialDiasAvisoAntes);
        Assert.False(valores.Convocacao.BloquearInadimplente); // R9: bloqueio desligado de fábrica
        Assert.False(valores.Presenca.FaltaJustificadaContaParaAlerta); // pendência 12.2: conta em separado
    }

    [Fact]
    public void Valor_gravado_vence_o_padrao()
    {
        var valores = new ValoresParametros(new Dictionary<string, string?> { [ChavesParametro.PresencaLimiteFaltas] = "5" });

        Assert.Equal(5, valores.Presenca.LimiteFaltas);
        Assert.Equal(15, valores.Presenca.JanelaAntiDuplicidadeDias);
    }

    [Theory]
    [InlineData(ChavesParametro.PresencaLimiteFaltas, "0")]
    [InlineData(ChavesParametro.PresencaLimiteFaltas, "31")]
    [InlineData(ChavesParametro.PresencaLimiteFaltas, "três")]
    [InlineData(ChavesParametro.FinanceiroMulta, "10,5")]
    [InlineData(ChavesParametro.FinanceiroReguaDiasDepois, "1, 5, 99")]
    [InlineData(ChavesParametro.MensagemJanelaSilencio, "21:00-21:00")]
    [InlineData(ChavesParametro.MensagemJanelaSilencio, "noite")]
    [InlineData(ChavesParametro.PresencaRegraFaltas, "Semanal")]
    [InlineData(ChavesParametro.ConvocacaoBloquearInadimplente, "talvez")]
    public void Valor_fora_do_tipo_ou_da_faixa_e_recusado(string chave, string entrada) =>
        Assert.False(ValidadorParametro.Validar(CatalogoParametros.ObterObrigatorio(chave), entrada).Valido);

    [Theory]
    [InlineData(ChavesParametro.FinanceiroMulta, "2,5", "2.5")]
    [InlineData(ChavesParametro.FinanceiroJuros, "1.25%", "1.25")]
    [InlineData(ChavesParametro.FinanceiroReguaDiasDepois, "10, 1,5 ,5", "1,5,10")]
    [InlineData(ChavesParametro.MensagemJanelaSilencio, "22:30 - 06:00", "22:30-06:00")]
    [InlineData(ChavesParametro.ConvocacaoSinalizarInadimplencia, "sim", "true")]
    [InlineData(ChavesParametro.PresencaRegraFaltas, "consecutivas", "Consecutivas")]
    public void A_digitacao_brasileira_e_normalizada(string chave, string entrada, string gravado)
    {
        var resultado = ValidadorParametro.Validar(CatalogoParametros.ObterObrigatorio(chave), entrada);

        Assert.True(resultado.Valido, resultado.Erro);
        Assert.Equal(gravado, resultado.Valor);
    }

    [Fact]
    public void Parametro_de_turma_sem_padrao_aceita_vazio_na_escolinha()
    {
        var mensalidade = CatalogoParametros.ObterObrigatorio(ChavesParametro.TurmaMensalidade);

        Assert.True(ValidadorParametro.Validar(mensalidade, "").Valido);
        Assert.Null(ValoresParametros.SoPadroes().Matricula.MensalidadeInicial);
    }
}

public class JanelaHorarioTestes
{
    [Theory]
    [InlineData("21:00", true)]
    [InlineData("23:59", true)]
    [InlineData("00:00", true)]
    [InlineData("06:59", true)]
    [InlineData("07:00", false)]
    [InlineData("12:00", false)]
    [InlineData("20:59", false)]
    public void Janela_que_vira_a_meia_noite(string hora, bool dentro) =>
        Assert.Equal(dentro, new JanelaHorario(new TimeOnly(21, 0), new TimeOnly(7, 0)).Contem(TimeOnly.Parse(hora)));

    [Fact]
    public void Janela_no_mesmo_dia()
    {
        var janela = new JanelaHorario(new TimeOnly(8, 0), new TimeOnly(20, 0));

        Assert.True(janela.Contem(new TimeOnly(8, 0)));
        Assert.False(janela.Contem(new TimeOnly(20, 0)));
        Assert.False(janela.ViraMeiaNoite);
    }
}

/// <summary>O gestor lê o parâmetro em pt-BR no editor e no histórico, nunca o texto invariável gravado.</summary>
public class FormatacaoParametroTestes
{
    [Theory]
    [InlineData(ChavesParametro.FinanceiroMulta, "2.5", "2,5 %")]
    [InlineData(ChavesParametro.FinanceiroReguaDiasDepois, "1,5,10", "1, 5, 10 dias depois")]
    [InlineData(ChavesParametro.PresencaFaltaJustificadaConta, "true", "ligado")]
    [InlineData(ChavesParametro.PresencaFaltaJustificadaConta, "false", "desligado")]
    [InlineData(ChavesParametro.ConvocacaoModoDisparo, "Imediato", "Na hora do disparo")]
    [InlineData(ChavesParametro.MensagemJanelaSilencio, "21:00-07:00", "21:00 às 07:00")]
    [InlineData(ChavesParametro.TurmaMensalidade, null, "—")]
    public void Valor_aparece_como_o_gestor_le(string chave, string? valor, string esperado) =>
        Assert.Equal(esperado, Formatacao.Parametro(CatalogoParametros.ObterObrigatorio(chave), valor));
}
