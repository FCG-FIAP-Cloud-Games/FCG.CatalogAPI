namespace FCG.Catalog.Application.Catalog.Jogos;

public enum StatusConsultaJogo
{
    Encontrado,
    IdInvalido,
    NaoEncontrado
}

/// <summary>
/// Dados devolvidos por consultas que retornam um único jogo.
/// </summary>
public sealed record JogoObtido(
    Guid Id,
    string Titulo,
    string? Descricao,
    string? FaixaEtaria,
    decimal Preco,
    bool Ativo,
    DateTimeOffset DataCadastro);

/// <summary>
/// Representa os resultados esperados da consulta de um jogo por identificador.
/// </summary>
public sealed class ResultadoObterJogo
{
    private ResultadoObterJogo(StatusConsultaJogo status, JogoObtido? jogo)
    {
        Status = status;
        Jogo = jogo;
    }

    public StatusConsultaJogo Status { get; }

    public JogoObtido? Jogo { get; }

    public static ResultadoObterJogo Encontrado(JogoObtido jogo) =>
        new(StatusConsultaJogo.Encontrado, jogo);

    public static ResultadoObterJogo IdInvalido() =>
        new(StatusConsultaJogo.IdInvalido, null);

    public static ResultadoObterJogo NaoEncontrado() =>
        new(StatusConsultaJogo.NaoEncontrado, null);
}
