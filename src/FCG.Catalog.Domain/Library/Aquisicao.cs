namespace FCG.Catalog.Domain.Library;

public sealed class Aquisicao
{
    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid JogoId { get; private set; }
    public DateTimeOffset DataAquisicao { get; private set; }
    public Guid? PedidoId { get; private set; }

    private Aquisicao() { }

    private Aquisicao(Guid id, Guid usuarioId, Guid jogoId, DateTimeOffset data, Guid? pedidoId)
    {
        if (id == Guid.Empty || usuarioId == Guid.Empty || jogoId == Guid.Empty || pedidoId == Guid.Empty)
            throw new ArgumentException("Identificadores da aquisição não podem ser vazios.");
        Id = id;
        UsuarioId = usuarioId;
        JogoId = jogoId;
        DataAquisicao = data.ToUniversalTime();
        PedidoId = pedidoId;
    }

    public static Aquisicao Conceder(Guid usuarioId, Guid jogoId, Guid pedidoId) =>
        new(Guid.NewGuid(), usuarioId, jogoId, DateTimeOffset.UtcNow, pedidoId);

    // Representação explícita de histórico; não executa importação ou concessão.
    public static Aquisicao Historica(Guid id, Guid usuarioId, Guid jogoId, DateTimeOffset dataAquisicao) =>
        new(id, usuarioId, jogoId, dataAquisicao, null);
}
