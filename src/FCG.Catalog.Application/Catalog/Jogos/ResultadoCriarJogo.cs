namespace FCG.Catalog.Application.Catalog.Jogos;

public enum StatusCriacaoJogo
{
    Criado,
    DadosInvalidos
}

/// <summary>
/// Dados devolvidos após a criação de um jogo.
/// </summary>
public sealed record JogoCriado(
    Guid Id,
    string Titulo,
    string? Descricao,
    string? FaixaEtaria,
    decimal Preco,
    bool Ativo,
    DateTimeOffset DataCadastro);

/// <summary>
/// Representa os resultados esperados do caso de uso sem acoplá-los a códigos HTTP.
/// </summary>
public sealed class ResultadoCriarJogo
{
    private ResultadoCriarJogo(
        StatusCriacaoJogo status,
        JogoCriado? jogo,
        IReadOnlyDictionary<string, string[]>? erros)
    {
        Status = status;
        Jogo = jogo;
        Erros = erros ?? new Dictionary<string, string[]>();
    }

    public StatusCriacaoJogo Status { get; }

    public JogoCriado? Jogo { get; }

    public IReadOnlyDictionary<string, string[]> Erros { get; }

    public static ResultadoCriarJogo Criado(JogoCriado jogo) =>
        new(StatusCriacaoJogo.Criado, jogo, null);

    public static ResultadoCriarJogo DadosInvalidos(
        IReadOnlyDictionary<string, string[]> erros) =>
        new(StatusCriacaoJogo.DadosInvalidos, null, erros);
}
