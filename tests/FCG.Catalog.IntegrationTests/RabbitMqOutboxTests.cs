using System.Net;
using System.Net.Sockets;
using System.Text;
using FCG.Catalog.Domain.Orders;
using FCG.Catalog.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Xunit;

namespace FCG.Catalog.IntegrationTests;

public sealed class RabbitMqFactAttribute : FactAttribute
{
    public RabbitMqFactAttribute()
    {
        var missing = new[] { "RABBITMQ_TEST_CONNECTION", "CATALOG_TEST_CONNECTION" }
            .Where(x => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(x))).ToArray();
        if (missing.Length > 0) Skip = "Configure " + string.Join(" e ", missing) + " (ambientes descartáveis).";
    }
}

public sealed class RabbitMqOutboxTests
{
    [RabbitMqFact]
    public async Task Broker_real_indisponibilidade_recuperacao_return_binding_e_confirm_antes_de_queda()
    {
        // Vhost EXCLUSIVO de testes, sem esses recursos. Nunca apagar recurso preexistente.
        var uri = new Uri(Environment.GetEnvironmentVariable("RABBITMQ_TEST_CONNECTION")!);
        if (uri.Scheme != "amqp") throw new InvalidOperationException("O proxy de falhas do teste requer URI amqp em ambiente local isolado.");
        var factory = new ConnectionFactory { Uri = uri, AutomaticRecoveryEnabled = false };
        await using var connection = await factory.CreateConnectionAsync();
        await using var admin = await connection.CreateChannelAsync();
        const string exchange = "OrderPlacedEvent";
        const string queue = "payments-order-placed";
        await RequireAbsent(connection, exchange, true);
        await RequireAbsent(connection, queue, false);
        var createdExchange = false; var createdQueue = false;
        try
        {
            await admin.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, true, false);
            createdExchange = true;
            await using var env = new OutboxDatabase(); await env.InitializeAsync();
            var p = await env.CriarAsync();
            await using var db = env.Open();
            var original = await db.Outbox.AsNoTracking().SingleAsync();
            await using var proxy = new BrokerProxy(factory.HostName, factory.Port);
            var options = Options.Create(new RabbitMqOptions
            {
                Host = "127.0.0.1", Port = proxy.Port, Username = factory.UserName,
                Password = factory.Password, VirtualHost = factory.VirtualHost, Exchange = exchange
            });
            await using var publisher = new RabbitMqPublisher(options);
            var repo = new RepositorioOutbox(db);
            var deliver = new EntregadorOutbox(repo, publisher, NullLogger<EntregadorOutbox>.Instance);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var ct = deadline.Token;

            // Broker inacessível pelo caminho TCP real usado pelo publisher, sem mock de AMQP.
            await deliver.ExecutarAsync(ct);
            var failed = await db.Outbox.AsNoTracking().SingleAsync(ct);
            Assert.Null(failed.PublishedAt); Assert.Equal(1, failed.Attempts);
            Assert.NotNull(failed.LastError); Assert.NotNull(failed.NextAttemptAt);
            Assert.Equal(StatusPedido.PendingPayment, (await db.Pedidos.AsNoTracking().SingleAsync(ct)).Status);

            // Recupera conectividade, mas mantém exchange SEM binding: mandatory provoca basic.return.
            proxy.Enabled = true;
            await Reagendar(db, original.Id);
            await deliver.ExecutarAsync(ct);
            var returned = await db.Outbox.AsNoTracking().SingleAsync(ct);
            Assert.Null(returned.PublishedAt); Assert.Equal(2, returned.Attempts);
            Assert.Equal("Unroutable", returned.LastError);

            // Corrige topologia. A mesma Outbox chega byte a byte à fila durável.
            await admin.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);
            createdQueue = true;
            await admin.QueueBindAsync(queue, exchange, "");
            await Reagendar(db, original.Id);
            await deliver.ExecutarAsync(ct);
            var done = await db.Outbox.AsNoTracking().SingleAsync(ct);
            Assert.NotNull(done.PublishedAt); Assert.Null(done.LockToken); Assert.Equal(3, done.Attempts);
            Assert.Equal(original.Payload, done.Payload);
            var received = await admin.BasicGetAsync(queue, autoAck: true, cancellationToken: ct);
            Assert.NotNull(received);
            Assert.Equal(Encoding.UTF8.GetBytes(original.Payload), received.Body.ToArray());
            Assert.Equal(original.EventId.ToString(), received.BasicProperties.MessageId);
            Assert.Equal(original.CorrelationId.ToString(), received.BasicProperties.CorrelationId);
            Assert.True(received.BasicProperties.Persistent);

            // Outra compra: confirmação real, mas interrupção antes de gravar PublishedAt.
            await env.CriarAsync();
            var reserved = Assert.Single(await repo.ReservarAsync(1, TimeSpan.FromMinutes(1), ct));
            await publisher.PublicarAsync(reserved, ct);
            Assert.Null((await db.Outbox.AsNoTracking().SingleAsync(x => x.Id == reserved.Id, ct)).PublishedAt);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE catalog_outbox SET \"LockedUntil\" = now() - interval '1 second' WHERE \"Id\" = {reserved.Id}", ct);
            await deliver.ExecutarAsync(ct);
            var copy1 = await admin.BasicGetAsync(queue, true, ct);
            var copy2 = await admin.BasicGetAsync(queue, true, ct);
            Assert.NotNull(copy1); Assert.NotNull(copy2);
            Assert.Equal(copy1.Body.ToArray(), copy2.Body.ToArray());
            Assert.Equal(reserved.EventId.ToString(), copy1.BasicProperties.MessageId);
            Assert.Equal(copy1.BasicProperties.MessageId, copy2.BasicProperties.MessageId);
            Assert.Equal(reserved.CorrelationId.ToString(), copy2.BasicProperties.CorrelationId);
            Assert.NotNull((await db.Outbox.AsNoTracking().SingleAsync(x => x.Id == reserved.Id, ct)).PublishedAt);
        }
        finally
        {
            if (createdQueue) await admin.QueueDeleteAsync(queue);
            if (createdExchange) await admin.ExchangeDeleteAsync(exchange);
        }
    }

    private static async Task Reagendar(FCG.Catalog.Infrastructure.Data.EF.Context.CatalogDbContext db, Guid id) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE catalog_outbox SET \"NextAttemptAt\" = now() - interval '1 second' WHERE \"Id\" = {id}");

    private static async Task RequireAbsent(IConnection connection, string name, bool exchange)
    {
        await using var probe = await connection.CreateChannelAsync();
        try
        {
            if (exchange) await probe.ExchangeDeclarePassiveAsync(name);
            else await probe.QueueDeclarePassiveAsync(name);
        }
        catch (RabbitMQ.Client.Exceptions.OperationInterruptedException ex) when (ex.ShutdownReason?.ReplyCode == 404) { return; }
        throw new InvalidOperationException("Use vhost exclusivo vazio para C17; recurso já existente: " + name);
    }

    // Proxy de transporte: permite falhar a conexão e depois encaminhar bytes ao RabbitMQ real.
    private sealed class BrokerProxy : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly List<Task> clients = [];
        private readonly Task loop;
        private readonly string host;
        private readonly int upstreamPort;
        public volatile bool Enabled;
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        public BrokerProxy(string host, int port)
        {
            this.host = host; upstreamPort = port; listener.Start(); loop = Accept();
        }
        private async Task Accept()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var incoming = await listener.AcceptTcpClientAsync(stop.Token);
                    if (!Enabled) { incoming.Dispose(); continue; }
                    clients.Add(Forward(incoming));
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }
        private async Task Forward(TcpClient incoming)
        {
            using (incoming)
            using (var outgoing = new TcpClient())
            using (var transfer = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
            {
                try
                {
                    await outgoing.ConnectAsync(host, upstreamPort, transfer.Token);
                    var a = incoming.GetStream().CopyToAsync(outgoing.GetStream(), transfer.Token);
                    var b = outgoing.GetStream().CopyToAsync(incoming.GetStream(), transfer.Token);
                    await Task.WhenAny(a, b); transfer.Cancel();
                    try { await Task.WhenAll(a, b); } catch (OperationCanceledException) { }
                }
                catch (IOException) { }
                catch (SocketException) { }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            }
        }
        public async ValueTask DisposeAsync()
        {
            stop.Cancel(); await loop; listener.Stop(); await Task.WhenAll(clients); stop.Dispose();
        }
    }
}
