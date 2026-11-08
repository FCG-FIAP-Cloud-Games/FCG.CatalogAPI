using System.Data.Common;
using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Infrastructure.IoC;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Nodes;
using FCG.Catalog.Application.Library;
using FCG.Catalog.Application.Orders;
using FCG.Catalog.Application.Payments;
using FCG.Catalog.Domain.Orders;
using FCG.Catalog.Infrastructure.Data.EF;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using FCG.Catalog.Infrastructure.Inbox;
using FCG.Catalog.Infrastructure.Repositories;
using FCG.Catalog.Infrastructure.Repositories.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FCG.Catalog.IntegrationTests;

internal static class PaymentTestSupport
{
    internal static ServiceDescriptor JogosDescriptor => new ServiceCollection()
        .AddCatalogInfrastructure(new ConfigurationBuilder().Build()).Single(d => d.ServiceType == typeof(IRepositorioJogos));

    public static PaymentProcessedEvent Event(Pedido p, string status = "Approved") => new()
    {
        EventId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), OccurredAt = DateTimeOffset.UtcNow,
        Version = 1, PaymentId = Guid.NewGuid(), OrderId = p.Id, UserId = p.UserId, GameId = p.GameId,
        Amount = p.Price, Currency = p.Currency, Status = status
    };
    public static byte[] Envelope(PaymentProcessedEvent e, Guid transportId)
    {
        var message = JsonSerializer.SerializeToNode(e, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        message["amount"] = e.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return JsonSerializer.SerializeToUtf8Bytes(new JsonObject
        {
            ["messageId"] = transportId.ToString(), ["correlationId"] = e.CorrelationId.ToString(),
            ["messageType"] = new JsonArray("urn:message:FCG.Payments.Application.Messaging:PaymentProcessedEvent"),
            ["message"] = message, ["headers"] = new JsonObject()
        });
    }
    public static ManipuladorPaymentProcessedEvent Handler(CatalogDbContext db, ILockUsuarioJogo? gate = null) =>
        new(new RepositorioProcessamentoPagamento(db), gate ?? new LockUsuarioJogo(db),
            new ManipuladorConcederJogoAoUsuario((IRepositorioJogos)Activator.CreateInstance(JogosDescriptor.ImplementationType!, db)!, new RepositorioPedidos(db), new RepositorioAquisicoes(db)));

    public static CatalogDbContext Open(string connection, params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).AddInterceptors(interceptors).Options);
}

// Bloqueia exatamente após SaveChanges e antes do COMMIT real.
internal sealed class BeforeCommit : DbTransactionInterceptor
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Fail { get; set; }
    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        Entered.TrySetResult();
        await Release.Task.WaitAsync(cancellationToken);
        if (Fail) throw new TimeoutException("Falha de teste antes do commit.");
        return result;
    }
}
