namespace FCG.Catalog.Application.Catalog.Jogos;

/// <summary>
/// Contém os dados recebidos pelo caso de uso de atualização de jogo.
/// </summary>
public sealed class ComandoAtualizarJogo
{
    public ComandoAtualizarJogo(
        Guid id,
        string titulo,
        string? descricao,
        string? faixaEtaria,
        decimal preco)
    {
        Id = id;
        Titulo = titulo;
        Descricao = descricao;
        FaixaEtaria = faixaEtaria;
        Preco = preco;
    }

    public Guid Id { get; }

    public string Titulo { get; }

    public string? Descricao { get; }

    public string? FaixaEtaria { get; }

    public decimal Preco { get; }
}
