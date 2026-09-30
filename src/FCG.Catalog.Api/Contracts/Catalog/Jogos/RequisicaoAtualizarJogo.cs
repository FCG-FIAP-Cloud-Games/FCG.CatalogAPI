namespace FCG.Catalog.Api.Contracts.Catalog.Jogos;

/// <summary>
/// Dados recebidos para atualizar um jogo existente no catálogo.
/// </summary>
public sealed class RequisicaoAtualizarJogo
{
    public string Titulo { get; init; } = string.Empty;

    public string? Descricao { get; init; }

    public string? FaixaEtaria { get; init; }

    public decimal Preco { get; init; }
}
