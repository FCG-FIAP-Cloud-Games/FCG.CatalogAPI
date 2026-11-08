namespace FCG.Catalog.Api.Contracts.Catalog.Jogos;

/// <summary>
/// Dados recebidos para criar um jogo no catálogo.
/// </summary>
public sealed class RequisicaoCriarJogo
{
    public string Titulo { get; init; } = string.Empty;

    public string? Descricao { get; init; }

    public string? FaixaEtaria { get; init; }

    public decimal Preco { get; init; }
}
