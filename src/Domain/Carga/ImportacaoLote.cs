using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Carga;

public enum SituacaoImportacao
{
    /// <summary>Enviada: a gestão mapeia as colunas e confere as linhas. Nada entrou no cadastro ainda.</summary>
    Aberta = 10,

    /// <summary>Confirmada: as linhas aptas entraram no cadastro, numa gravação só (BL-M12-04).</summary>
    Confirmada = 20,

    /// <summary>Descartada pela gestão, ou vencida sem confirmação: nada entrou.</summary>
    Descartada = 30
}

/// <summary>
/// Um envio de planilha da carga inicial (S7, BL-M12-01 a BL-M12-05). A planilha lida (cabeçalhos e células, em texto)
/// fica cifrada enquanto o lote está aberto e é apagada na confirmação ou no descarte: dado de saúde e CPF não ficam
/// guardados fora do cadastro (R5). O que fica é o resumo, com quem enviou, quem encerrou e quando.
/// </summary>
public class ImportacaoLote : IEntidadeDaEscolinha
{
    public const int TamanhoArquivo = 200;
    public const int TamanhoNome = 150;

    /// <summary>O arquivo maior que isso não é lido (a carga do piloto tem cerca de 60 alunos).</summary>
    public const long MaximoBytes = 2 * 1024 * 1024;
    public const int MaximoLinhas = 1000;
    public const int MaximoColunas = 60;

    /// <summary>O lote aberto há mais dias que isso é descartado na próxima importação da escolinha.</summary>
    public const int DiasAberto = 7;

    public Guid ImportacaoLote_ID { get; set; }
    public Guid Escolinha_ID { get; set; }

    public string NomeArquivo { get; private set; } = string.Empty;

    /// <summary>Quantas linhas com dado a planilha tem (sem o cabeçalho).</summary>
    public int Linhas { get; private set; }

    public SituacaoImportacao Situacao { get; private set; } = SituacaoImportacao.Aberta;

    /// <summary>A planilha lida, cifrada (Data Protection). <c>null</c> depois da confirmação ou do descarte.</summary>
    public string? Conteudo { get; private set; }

    /// <summary>A coluna de cada campo (JSON), escolhida pela gestão; nasce com o que o cabeçalho sugere.</summary>
    public string Mapeamento { get; private set; } = "{}";

    /// <summary>As decisões da conferência (JSON): o de-para das turmas, os alunos parecidos e os responsáveis reaproveitados.</summary>
    public string Decisoes { get; private set; } = "{}";

    public DateTime EnviadoEm { get; private set; }
    public Guid? EnviadoPor_ID { get; private set; }
    public string EnviadoPorNome { get; private set; } = string.Empty;

    public DateTime? EncerradoEm { get; private set; }
    public Guid? EncerradoPor_ID { get; private set; }
    public string? EncerradoPorNome { get; private set; }

    /// <summary>O resumo da confirmação.</summary>
    public int AlunosCriados { get; private set; }
    public int ResponsaveisCriados { get; private set; }
    public int ResponsaveisReaproveitados { get; private set; }
    public int LinhasNaoImportadas { get; private set; }

    public byte[] Versao { get; set; } = [];

    public bool Aberta => Situacao == SituacaoImportacao.Aberta;

    public static ImportacaoLote Nova(string arquivo, string conteudoProtegido, int linhas, string mapeamento, DateTime agora,
        Guid? autorId, string autorNome) => new()
    {
        ImportacaoLote_ID = Guid.NewGuid(),
        NomeArquivo = TextoHelper.Truncar(arquivo.Trim(), TamanhoArquivo)!,
        Conteudo = conteudoProtegido,
        Linhas = linhas,
        Mapeamento = mapeamento,
        EnviadoEm = agora,
        EnviadoPor_ID = autorId,
        EnviadoPorNome = TextoHelper.Truncar(autorNome, TamanhoNome)!
    };

    public void DefinirMapeamento(string mapeamento)
    {
        ExigirAberta();
        Mapeamento = mapeamento;
    }

    public void DefinirDecisoes(string decisoes)
    {
        ExigirAberta();
        Decisoes = decisoes;
    }

    /// <summary>As linhas aptas entraram no cadastro: o conteúdo da planilha é apagado, fica o resumo.</summary>
    public void Confirmar(DateTime agora, Guid? autorId, string autorNome, int alunos, int responsaveisNovos, int reaproveitados, int naoImportadas)
    {
        Encerrar(SituacaoImportacao.Confirmada, agora, autorId, autorNome);
        AlunosCriados = alunos;
        ResponsaveisCriados = responsaveisNovos;
        ResponsaveisReaproveitados = reaproveitados;
        LinhasNaoImportadas = naoImportadas;
    }

    public void Descartar(DateTime agora, Guid? autorId, string autorNome) =>
        Encerrar(SituacaoImportacao.Descartada, agora, autorId, autorNome);

    private void Encerrar(SituacaoImportacao para, DateTime agora, Guid? autorId, string autorNome)
    {
        ExigirAberta();
        Situacao = para;
        Conteudo = null;
        EncerradoEm = agora;
        EncerradoPor_ID = autorId;
        EncerradoPorNome = TextoHelper.Truncar(autorNome, TamanhoNome);
    }

    private void ExigirAberta()
    {
        if (!Aberta) throw new InvalidOperationException("O lote já foi encerrado.");
    }
}
