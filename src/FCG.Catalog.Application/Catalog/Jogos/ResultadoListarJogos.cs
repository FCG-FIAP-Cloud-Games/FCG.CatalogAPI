namespace FCG.Catalog.Application.Catalog.Jogos;

public enum StatusListagemJogos
{
    Sucesso,
    PaginacaoInvalida
}

/// <summary>
/// Representa os resultados esperados da listagem paginada do catálogo de jogos.
/// </summary>
public sealed class ResultadoListarJogos
{
    private ResultadoListarJogos(
        StatusListagemJogos status,
        IReadOnlyList<JogoObtido>? itens,
        int pagina,
        int tamanhoPagina,
        IReadOnlyDictionary<string, string[]>? erros)
    {
        Status = status;
        Itens = itens ?? [];
        Pagina = pagina;
        TamanhoPagina = tamanhoPagina;
        Erros = erros ?? new Dictionary<string, string[]>();
    }

    public StatusListagemJogos Status { get; }

    public IReadOnlyList<JogoObtido> Itens { get; }

    public int Pagina { get; }

    public int TamanhoPagina { get; }

    public IReadOnlyDictionary<string, string[]> Erros { get; }

    public static ResultadoListarJogos Sucesso(
        IReadOnlyList<JogoObtido> itens,
        int pagina,
        int tamanhoPagina) =>
        new(StatusListagemJogos.Sucesso, itens, pagina, tamanhoPagina, null);

    public static ResultadoListarJogos PaginacaoInvalida(
        IReadOnlyDictionary<string, string[]> erros) =>
        new(StatusListagemJogos.PaginacaoInvalida, null, 0, 0, erros);
}
