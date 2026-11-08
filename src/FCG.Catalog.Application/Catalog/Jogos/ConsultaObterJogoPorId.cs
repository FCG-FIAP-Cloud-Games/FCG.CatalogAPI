namespace FCG.Catalog.Application.Catalog.Jogos;

/// <summary>
/// Contém os dados recebidos pelo caso de uso de consulta de jogo por identificador.
/// </summary>
public sealed class ConsultaObterJogoPorId
{
    public ConsultaObterJogoPorId(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; }
}
