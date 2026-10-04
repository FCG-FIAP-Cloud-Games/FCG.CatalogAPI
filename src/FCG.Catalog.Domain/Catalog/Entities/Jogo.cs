using System;

namespace FCG.Catalog.Domain.Catalog.Entities;

public sealed class Jogo
{
    public Guid Id { get; private set; }
    public string Titulo { get; private set; }
    public string? Descricao { get; private set; }
    public string? FaixaEtaria { get; private set; }
    public decimal Preco { get; private set; }
    public bool Ativo { get; private set; }
    public DateTimeOffset DataCadastro { get; private set; }

    private Jogo()
    {
        Titulo = string.Empty;
    } 

    public Jogo(Guid id, string titulo, string? descricao, string? faixaEtaria, decimal preco)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("O identificador do jogo não pode ser vazio.");

        if (string.IsNullOrWhiteSpace(titulo))
            throw new ArgumentException("O título do jogo é obrigatório.");

        if (preco < 0)
            throw new ArgumentException("O preço do jogo não pode ser negativo.");

        Id = id;
        Titulo = titulo;
        Descricao = descricao;
        FaixaEtaria = faixaEtaria;
        Preco = preco;
        Ativo = true;
        DataCadastro = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Atualiza os dados alteráveis do jogo, preservando as invariantes de domínio.
    /// </summary>
    public void AtualizarDados(string titulo, string? descricao, string? faixaEtaria, decimal preco)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new ArgumentException("O título do jogo é obrigatório.");

        if (preco < 0)
            throw new ArgumentException("O preço do jogo não pode ser negativo.");

        Titulo = titulo;
        Descricao = descricao;
        FaixaEtaria = faixaEtaria;
        Preco = preco;
    }
}
