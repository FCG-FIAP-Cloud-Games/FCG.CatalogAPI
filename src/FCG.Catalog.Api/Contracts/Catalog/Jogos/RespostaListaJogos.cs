namespace FCG.Catalog.Api.Contracts.Catalog.Jogos;

public sealed record RespostaListaJogos(
    IReadOnlyList<RespostaJogo> Itens,
    int Pagina,
    int TamanhoPagina);
