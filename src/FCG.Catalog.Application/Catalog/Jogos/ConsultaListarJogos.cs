namespace FCG.Catalog.Application.Catalog.Jogos;

/// <summary>
/// Contém os dados recebidos pelo caso de uso de listagem paginada do catálogo de jogos.
/// </summary>
public sealed class ConsultaListarJogos
{
    public ConsultaListarJogos(int pagina, int tamanhoPagina)
    {
        Pagina = pagina;
        TamanhoPagina = tamanhoPagina;
    }

    public int Pagina { get; }

    public int TamanhoPagina { get; }
}
