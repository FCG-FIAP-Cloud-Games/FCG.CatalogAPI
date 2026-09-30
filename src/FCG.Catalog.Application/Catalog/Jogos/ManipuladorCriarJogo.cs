using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Domain.Catalog.Entities;

namespace FCG.Catalog.Application.Catalog.Jogos;

/// <summary>
/// Valida, normaliza e persiste um novo jogo do catálogo.
/// </summary>
public sealed class ManipuladorCriarJogo
{
    public const int TamanhoMaximoTitulo = 150;

    private readonly IRepositorioJogos _repositorioJogos;

    public ManipuladorCriarJogo(IRepositorioJogos repositorioJogos)
    {
        _repositorioJogos = repositorioJogos;
    }

    public async Task<ResultadoCriarJogo> ProcessarAsync(
        ComandoCriarJogo comando,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var tituloNormalizado = comando.Titulo?.Trim() ?? string.Empty;
        var erros = Validar(tituloNormalizado, comando.Preco);

        if (erros.Count > 0)
        {
            return ResultadoCriarJogo.DadosInvalidos(erros);
        }

        var jogo = new Jogo(
            Guid.NewGuid(),
            tituloNormalizado,
            comando.Descricao?.Trim(),
            comando.FaixaEtaria?.Trim(),
            comando.Preco);

        await _repositorioJogos.AdicionarAsync(jogo, cancellationToken);

        return ResultadoCriarJogo.Criado(new JogoCriado(
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
        else if (tituloNormalizado.Length > TamanhoMaximoTitulo)
        {
            erros["titulo"] = [$"O título deve conter no máximo {TamanhoMaximoTitulo} caracteres."];
        }

        if (preco < 0)
        {
            erros["preco"] = ["O preço do jogo não pode ser negativo."];
        }

        return erros;
    }
}
