using FCG.Catalog.Application.Abstractions.Repositories;

namespace FCG.Catalog.Application.Catalog.Jogos;

/// <summary>
/// Valida e persiste a atualização dos dados alteráveis de um jogo existente.
/// </summary>
public sealed class ManipuladorAtualizarJogo
{
    private readonly IRepositorioJogos _repositorioJogos;

    public ManipuladorAtualizarJogo(IRepositorioJogos repositorioJogos)
    {
        _repositorioJogos = repositorioJogos;
    }

    public async Task<ResultadoAtualizarJogo> ProcessarAsync(
        ComandoAtualizarJogo comando,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var tituloNormalizado = comando.Titulo?.Trim() ?? string.Empty;
        var erros = Validar(tituloNormalizado, comando.Preco);

        if (erros.Count > 0)
        {
            return ResultadoAtualizarJogo.DadosInvalidos(erros);
        }

        var jogo = await _repositorioJogos.ObterPorIdAsync(comando.Id, cancellationToken);

        if (jogo is null)
        {
            return ResultadoAtualizarJogo.NaoEncontrado();
        }

        jogo.AtualizarDados(
            tituloNormalizado,
            comando.Descricao?.Trim(),
            comando.FaixaEtaria?.Trim(),
            comando.Preco);

        await _repositorioJogos.AtualizarAsync(jogo, cancellationToken);

        return ResultadoAtualizarJogo.Atualizado(new JogoObtido(
            jogo.Id,
            jogo.Titulo,
            jogo.Descricao,
            jogo.FaixaEtaria,
            jogo.Preco,
            jogo.Ativo,
            jogo.DataCadastro));
    }

    private static Dictionary<string, string[]> Validar(string tituloNormalizado, decimal preco)
    {
        var erros = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(tituloNormalizado))
        {
            erros["titulo"] = ["O título do jogo é obrigatório."];
        }
        else if (tituloNormalizado.Length > ManipuladorCriarJogo.TamanhoMaximoTitulo)
        {
            erros["titulo"] =
                [$"O título deve conter no máximo {ManipuladorCriarJogo.TamanhoMaximoTitulo} caracteres."];
        }

        if (preco < 0)
        {
            erros["preco"] = ["O preço do jogo não pode ser negativo."];
        }

        return erros;
    }
}
