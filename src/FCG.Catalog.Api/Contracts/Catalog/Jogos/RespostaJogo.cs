namespace FCG.Catalog.Api.Contracts.Catalog.Jogos;

public sealed record RespostaJogo(
    Guid Id,
    string Titulo,
    string? Descricao,
    string? FaixaEtaria,
    decimal Preco,
    bool Ativo,
    DateTimeOffset DataCadastro);
