namespace FCG.Catalog.Application.Catalog.Jogos;

public enum StatusAtualizacaoJogo
{
    Atualizado,
    DadosInvalidos,
    NaoEncontrado
}

/// <summary>
/// Representa os resultados esperados do caso de uso de atualização de jogo.
/// </summary>
public sealed class ResultadoAtualizarJogo
{
    private ResultadoAtualizarJogo(
        StatusAtualizacaoJogo status,
        JogoObtido? jogo,
        IReadOnlyDictionary<string, string[]>? erros)
    {
        Status = status;
        Jogo = jogo;
        Erros = erros ?? new Dictionary<string, string[]>();
    }

    public StatusAtualizacaoJogo Status { get; }

    public JogoObtido? Jogo { get; }

    public IReadOnlyDictionary<string, string[]> Erros { get; }

    public static ResultadoAtualizarJogo Atualizado(JogoObtido jogo) =>
        new(StatusAtualizacaoJogo.Atualizado, jogo, null);

    public static ResultadoAtualizarJogo DadosInvalidos(
        IReadOnlyDictionary<string, string[]> erros) =>
        new(StatusAtualizacaoJogo.DadosInvalidos, null, erros);

    public static ResultadoAtualizarJogo NaoEncontrado() =>
        new(StatusAtualizacaoJogo.NaoEncontrado, null, null);
}
