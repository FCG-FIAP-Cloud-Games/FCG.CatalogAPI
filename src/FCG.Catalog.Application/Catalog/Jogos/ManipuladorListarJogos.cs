using FCG.Catalog.Application.Abstractions.Repositories;

namespace FCG.Catalog.Application.Catalog.Jogos;

/// <summary>
/// Lista os jogos do catálogo de forma paginada.
/// </summary>
public sealed class ManipuladorListarJogos
{
    public const int TamanhoMaximoPagina = 100;

    private readonly IRepositorioJogos _repositorioJogos;

    public ManipuladorListarJogos(IRepositorioJogos repositorioJogos)
    {
        _repositorioJogos = repositorioJogos;
    }

    public async Task<ResultadoListarJogos> ProcessarAsync(
        ConsultaListarJogos consulta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var erros = Validar(consulta);

        if (erros.Count > 0)
        {
            return ResultadoListarJogos.PaginacaoInvalida(erros);
        }

        var jogos = await _repositorioJogos.ListarAsync(
            consulta.Pagina,
            consulta.TamanhoPagina,
            cancellationToken);

        var itens = jogos
            .Select(jogo => new JogoObtido(
                jogo.Id,
                jogo.Titulo,
                jogo.Descricao,
                jogo.FaixaEtaria,
                jogo.Preco,
                jogo.Ativo,
                jogo.DataCadastro))
            .ToList();

        return ResultadoListarJogos.Sucesso(itens, consulta.Pagina, consulta.TamanhoPagina);
    }

    private static Dictionary<string, string[]> Validar(ConsultaListarJogos consulta)
    {
        var erros = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (consulta.Pagina <= 0)
        {
            erros["pagina"] = ["A página deve ser maior que zero."];
        }

        if (consulta.TamanhoPagina <= 0)
        {
            erros["tamanhoPagina"] = ["O tamanho da página deve ser maior que zero."];
        }
        else if (consulta.TamanhoPagina > TamanhoMaximoPagina)
        {
            erros["tamanhoPagina"] =
                [$"O tamanho da página deve ser no máximo {TamanhoMaximoPagina}."];
        }

        return erros;
    }
}
