namespace FCG.Catalog.Application.Catalog.Jogos;

/// <summary>
/// Contém os dados recebidos pelo caso de uso de criação de jogo.
/// </summary>
public sealed class ComandoCriarJogo
{
    public ComandoCriarJogo(string titulo, string? descricao, string? faixaEtaria, decimal preco)
    {
        Titulo = titulo;
        Descricao = descricao;
        FaixaEtaria = faixaEtaria;
        Preco = preco;
    }

    public string Titulo { get; }

    public string? Descricao { get; }

    public string? FaixaEtaria { get; }

    public decimal Preco { get; }
}
