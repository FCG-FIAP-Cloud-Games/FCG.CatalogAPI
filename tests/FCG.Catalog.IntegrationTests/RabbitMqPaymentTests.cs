using System.Diagnostics;
using System.Net;
using System.Text;
using FCG.Catalog.Api.IoC;
using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Application.Orders;
using FCG.Catalog.Application.Payments;
using FCG.Catalog.Domain.Orders;
using FCG.Catalog.Infrastructure.Data.EF;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using FCG.Catalog.Infrastructure.IoC;
using FCG.Catalog.Infrastructure.Messaging;
using FCG.Catalog.Infrastructure.Outbox;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Xunit;

namespace FCG.Catalog.IntegrationTests;

public sealed class RabbitMqPaymentTests
{
    [RabbitMqFact]
    public async Task Approved_rejected_duplicates_contradicoes_invalidos_e_ACK_reais()
    {
        await using var f = await BrokerFixture.Create();
        await using var db = f.Database.Open();
        foreach (var status in new[] { "Approved", "Rejected" })
        {
            var p = await PostgreSqlBibliotecaTests.Seed(db);
            var e = PaymentTestSupport.Event(p, status);
            var body = PaymentTestSupport.Envelope(e, Guid.NewGuid());
            await f.Publish(e, body); await f.Publish(e, body);
            await f.WaitInbox(1 + (status == "Rejected" ? 2 : 0));
            var repeat = e with { EventId = Guid.NewGuid() };
            await f.Publish(repeat);
            await f.WaitInbox(status == "Approved" ? 2 : 4);
            var stored = await db.Pedidos.AsNoTracking().SingleAsync(x => x.Id == p.Id);
            var updated = stored.UpdatedAt;
            Assert.Equal(status == "Approved" ? StatusPedido.Paid : StatusPedido.Rejected, stored.Status);
            var contradiction = e with { EventId = Guid.NewGuid(), Status = status == "Approved" ? "Rejected" : "Approved" };
            await f.Publish(contradiction);
            var error = await f.Error();
            Assert.Equal(PaymentTestSupport.Envelope(contradiction, f.TransportId), error.Body.ToArray());
            Assert.Equal(1, error.BasicProperties.Headers![PaymentProcessedConsumer.AttemptHeader]);
            Assert.Equal("ResultadoContraditorio", Header(error, PaymentProcessedConsumer.ErrorHeader));
            Assert.Equal(updated, (await db.Pedidos.AsNoTracking().SingleAsync(x => x.Id == p.Id)).UpdatedAt);
        }
        Assert.Equal(4, await db.Inbox.CountAsync()); Assert.Single(await db.Aquisicoes.ToListAsync());
        var invalid = Encoding.UTF8.GetBytes("{\"messageId\":\"broken\",\"message\":null}");
        await f.Publish(null, invalid);
        var dead = await f.Error();
        Assert.Equal(invalid, dead.Body.ToArray());
        Assert.Equal(1, dead.BasicProperties.Headers![PaymentProcessedConsumer.AttemptHeader]);
        Assert.True(dead.BasicProperties.Persistent);
        Assert.Equal(AdaptadorPaymentProcessedEvent.ContentType, dead.BasicProperties.ContentType);
        Assert.Equal(f.TransportId.ToString(), dead.BasicProperties.MessageId);
        Assert.Equal("preservado", Header(dead, "teste-header"));
        await f.Stop();
        Assert.Equal(0u, await f.Channel.MessageCountAsync(f.Options.PaymentProcessedQueue));
        Assert.Null(await f.Channel.BasicGetAsync(f.Options.PaymentProcessedQueue, true));
    }

    [RabbitMqFact]
    public async Task Retry_limitado_sucesso_posterior_e_esgotamento_preservam_body_e_propriedades()
    {
        await using var f = await BrokerFixture.Create();
        await using var db = f.Database.Open();
        var p = await PostgreSqlBibliotecaTests.Seed(db); var e = PaymentTestSupport.Event(p);
        f.Failures.Remaining = 2;
        await f.Publish(e); await f.WaitInbox(1);
        Assert.Equal(3, f.Failures.Calls);
        Assert.Equal(StatusPedido.Paid, (await db.Pedidos.AsNoTracking().SingleAsync()).Status);
        var next = await PostgreSqlBibliotecaTests.Seed(db); var rejectedByInfra = PaymentTestSupport.Event(next);
        f.Failures.Remaining = 10; f.Failures.Calls = 0;
        await f.Publish(rejectedByInfra);
        var error = await f.Error();
        Assert.Equal(3, f.Failures.Calls);
        Assert.Equal(3, error.BasicProperties.Headers![PaymentProcessedConsumer.AttemptHeader]);
        Assert.Equal(PaymentTestSupport.Envelope(rejectedByInfra, f.TransportId), error.Body.ToArray());
        Assert.Equal(rejectedByInfra.CorrelationId.ToString(), error.BasicProperties.CorrelationId);
        Assert.Equal(f.TransportId.ToString(), error.BasicProperties.MessageId);
        Assert.Equal(StatusPedido.PendingPayment, (await db.Pedidos.AsNoTracking().SingleAsync(x => x.Id == next.Id)).Status);
        Assert.Single(await db.Inbox.ToListAsync()); Assert.Single(await db.Aquisicoes.ToListAsync());
        await f.Stop();
        Assert.Equal(0u, await f.Channel.MessageCountAsync(f.Options.PaymentProcessedQueue));
    }

    [RabbitMqFact]
    public async Task Interrupcao_antes_commit_sem_ACK_redelivery_apos_rollback()
    {
        var gate = new BeforeCommit();
        await using var f = await BrokerFixture.Create(gate);
        await using var db = f.Database.Open(); var p = await PostgreSqlBibliotecaTests.Seed(db);
        var e = PaymentTestSupport.Event(p);
        await f.Publish(e);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Empty(await db.Inbox.ToListAsync()); Assert.Empty(await db.Aquisicoes.ToListAsync());
        Assert.Equal(StatusPedido.PendingPayment, (await db.Pedidos.AsNoTracking().SingleAsync()).Status);
        await f.Stop();
        await BrokerFixture.Until(async () => await f.Channel.MessageCountAsync(f.Options.PaymentProcessedQueue) == 1);
        Assert.Empty(await db.Inbox.ToListAsync()); Assert.Empty(await db.Aquisicoes.ToListAsync());
        gate.Release.TrySetResult();
        await f.Start(); await f.WaitInbox(1);
        await f.Stop();
        Assert.Equal(0u, await f.Channel.MessageCountAsync(f.Options.PaymentProcessedQueue));
        Assert.Equal(StatusPedido.Paid, (await db.Pedidos.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(p.Id, (await db.Aquisicoes.SingleAsync()).PedidoId);
    }

    [RabbitMqFact]
    public async Task Mensagem_Approved_concorre_com_POST_usando_o_mesmo_lock()
    {
        var gate = new BeforeCommit();
        await using var f = await BrokerFixture.Create(gate);
        await using var db = f.Database.Open(); var p = await PostgreSqlBibliotecaTests.Seed(db);
        await f.Publish(PaymentTestSupport.Event(p));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var tokens = new CatalogFactory();
        using var host = tokens.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:CatalogDatabase", f.Database.ConnectionString);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRepositorioJogos>(); services.Add(PaymentTestSupport.JogosDescriptor);
                services.RemoveAll<ILockUsuarioJogo>();
                services.AddScoped<ILockUsuarioJogo>(s => new PostgreSqlPaymentTests.ObservedLock(
                    new LockUsuarioJogo(s.GetRequiredService<CatalogDbContext>()), attempted));
            });
        });
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.Token(role: "Usuario", userId: p.UserId));
        var post = PedidosApiTests.Post(client, p.GameId, Guid.NewGuid().ToString());
        try { await attempted.Task.WaitAsync(TimeSpan.FromSeconds(15)); Assert.False(post.IsCompleted); }
        finally { gate.Release.TrySetResult(); }
        using var response = await post.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await f.WaitInbox(1); await f.Stop();
        Assert.Single(await db.Pedidos.ToListAsync()); Assert.Single(await db.Aquisicoes.ToListAsync());
        Assert.Equal(0u, await f.Channel.MessageCountAsync(f.Options.PaymentProcessedQueue));
    }

    [RabbitMqFact]
    public async Task Consumer_recupera_cancelamento_do_broker()
    {
        await using var f = await BrokerFixture.Create();
        await f.Channel.QueueDeleteAsync(f.Options.PaymentProcessedQueue);
        // A sessão detecta basic.cancel e recria topologia/conexão.
        await Task.Delay(2500);
        await BrokerFixture.Until(async () => (await f.Channel.QueueDeclareAsync(
            f.Options.PaymentProcessedQueue, true, false, false)).ConsumerCount == 1);
        await using var db = f.Database.Open(); var p = await PostgreSqlBibliotecaTests.Seed(db);
        await f.Publish(PaymentTestSupport.Event(p)); await f.WaitInbox(1);
    }

    // Executável externo opcional que usa o publisher Payments real; sem referência
    // de assembly ou pacote MassTransit em nenhum projeto do Catalog.
    [PaymentsPublisherFact]
    public async Task Publisher_Payments_real_MassTransit_para_consumer_Catalog()
    {
        await using var f = await BrokerFixture.CreateOfficial();
        await using var db = f.Database.Open(); var p = await PostgreSqlBibliotecaTests.Seed(db);
        var e = PaymentTestSupport.Event(p);
        var capture = "capture-" + Guid.NewGuid().ToString("N");
        await f.Channel.QueueDeclareAsync(capture, false, false, true);
        await f.Channel.QueueBindAsync(capture, f.Options.PaymentProcessedExchange, "");
        try
        {
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Environment.GetEnvironmentVariable("PAYMENTS_TEST_PUBLISHER")!);
            start.Environment["PAYMENTS_TEST_EXCHANGE"] = f.Options.PaymentProcessedExchange;
            using var process = Process.Start(start)!;
            await process.StandardInput.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(e));
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(process.ExitCode == 0, "Publisher externo falhou; saída omitida para preservar credenciais.");
            await Task.WhenAll(output, errors);
            BasicGetResult? delivered = null;
            await BrokerFixture.Until(async () => (delivered = await f.Channel.BasicGetAsync(capture, true)) is not null);
            Assert.Equal(AdaptadorPaymentProcessedEvent.ContentType, delivered!.BasicProperties.ContentType);
            Assert.True(delivered.BasicProperties.Persistent);
            Assert.Equal(e.CorrelationId.ToString(), delivered.BasicProperties.CorrelationId);
            Assert.NotEqual(e.EventId.ToString(), delivered.BasicProperties.MessageId);
            using var json = System.Text.Json.JsonDocument.Parse(delivered.Body);
            Assert.Equal(System.Text.Json.JsonValueKind.String, json.RootElement.GetProperty("message").GetProperty("amount").ValueKind);
            Assert.Equal(e, AdaptadorPaymentProcessedEvent.Ler(delivered.Body.Span, delivered.BasicProperties.ContentType));
            await f.WaitInbox(1); await f.Stop();
            Assert.Equal(StatusPedido.Paid, (await db.Pedidos.AsNoTracking().SingleAsync()).Status);
            Assert.Equal(p.Id, (await db.Aquisicoes.SingleAsync()).PedidoId);
            Assert.Equal(e.EventId, (await db.Inbox.SingleAsync()).EventId);
            Assert.Equal(0u, await f.Channel.MessageCountAsync(f.Options.PaymentProcessedQueue));
        }
        finally { await f.Channel.QueueDeleteAsync(capture); }
    }

    private static string Header(BasicGetResult e, string name) =>
        Encoding.UTF8.GetString((byte[])e.BasicProperties.Headers![name]!);

    private sealed class AckLog : Microsoft.Extensions.Logging.ILogger<PaymentProcessedConsumer>
    {
        public System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> Events { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel level, Microsoft.Extensions.Logging.EventId id,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> values) return;
            var fields = values.ToDictionary(x => x.Key, x => x.Value);
            if (fields.GetValueOrDefault("Resultado") is "Processado" or "Duplicado" or "Compativel"
                && fields.GetValueOrDefault("EventId") is Guid eventId) Events.TryAdd(eventId, 0);
        }
    }

    private sealed class Failures { public int Remaining; public int Calls; }
    private sealed class ObservedProcessor(ManipuladorPaymentProcessedEvent inner, Failures failures,
        IContextoCorrelacao correlation) : IProcessadorPagamento
    {
        public Task<ResultadoPagamento> ExecutarAsync(PaymentProcessedEvent e, CancellationToken ct)
        {
            Assert.Equal(e.CorrelationId, correlation.CorrelationId);
            Interlocked.Increment(ref failures.Calls);
            if (Interlocked.Decrement(ref failures.Remaining) >= 0) throw new TimeoutException();
            return inner.ExecutarAsync(e, ct);
        }
    }

    private sealed class BrokerFixture : IAsyncDisposable
    {
        public BibliotecaDatabase Database { get; } = new();
        public RabbitMqOptions Options { get; private set; } = null!;
        public Failures Failures { get; } = new();
        public Guid TransportId { get; } = Guid.NewGuid();
        public IChannel Channel { get; private set; } = null!;
        private IConnection connection = null!;
        private ServiceProvider provider = null!;
        private readonly AckLog acknowledgments = new();
        private PaymentProcessedConsumer? worker;
        private bool ownsExchange = true;
        public static Task<BrokerFixture> Create(params IInterceptor[] interceptors) => CreateCore(false, interceptors);
        public static Task<BrokerFixture> CreateOfficial() => CreateCore(true, []);
        private static async Task<BrokerFixture> CreateCore(bool official, IInterceptor[] interceptors)
        {
            var f = new BrokerFixture();
            await f.Database.InitializeAsync();
            var factory = new ConnectionFactory { Uri = new Uri(Environment.GetEnvironmentVariable("RABBITMQ_TEST_CONNECTION")!), AutomaticRecoveryEnabled = false };
            f.connection = await factory.CreateConnectionAsync();
            f.Channel = await f.connection.CreateChannelAsync(new CreateChannelOptions(true, true));
            var id = Guid.NewGuid().ToString("N");
            f.Options = new RabbitMqOptions { Host = factory.HostName, Port = factory.Port,
                Username = factory.UserName, Password = factory.Password, VirtualHost = factory.VirtualHost,
                PaymentProcessedExchange = official ? "PaymentProcessedEvent" : "PaymentProcessedEvent.test." + id,
                PaymentProcessedQueue = "catalog-payment-processed.test." + id,
                PaymentProcessedErrorQueue = "catalog-payment-processed-error.test." + id };
            if (official)
            {
                await using var probe = await f.connection.CreateChannelAsync();
                try { await probe.ExchangeDeclarePassiveAsync(f.Options.PaymentProcessedExchange); f.ownsExchange = false; }
                catch (RabbitMQ.Client.Exceptions.OperationInterruptedException ex) when (ex.ShutdownReason?.ReplyCode == 404) { }
            }
            await f.Channel.ExchangeDeclareAsync(f.Options.PaymentProcessedExchange, ExchangeType.Fanout, true, false);
            await f.Channel.QueueDeclareAsync(f.Options.PaymentProcessedQueue, true, false, false);
            await f.Channel.QueueDeclareAsync(f.Options.PaymentProcessedErrorQueue, true, false, false);
            await f.Channel.QueueBindAsync(f.Options.PaymentProcessedQueue, f.Options.PaymentProcessedExchange, "");
            var services = new ServiceCollection();
            services.AddLogging(); services.AddCatalogApplication();
            services.AddCatalogInfrastructure(new ConfigurationBuilder().Build());
            services.AddScoped(_ => PaymentTestSupport.Open(f.Database.ConnectionString, interceptors));
            services.AddScoped<ManipuladorPaymentProcessedEvent>();
            services.AddScoped<IProcessadorPagamento>(s => new ObservedProcessor(s.GetRequiredService<ManipuladorPaymentProcessedEvent>(),
                f.Failures, s.GetRequiredService<IContextoCorrelacao>()));
            f.provider = services.BuildServiceProvider();
            await f.Start();
            return f;
        }
        public async Task Start()
        {
            worker = new PaymentProcessedConsumer(provider.GetRequiredService<IServiceScopeFactory>(),
                Microsoft.Extensions.Options.Options.Create(Options), acknowledgments);
            await worker.StartAsync(CancellationToken.None);
            await Until(async () => (await Channel.QueueDeclarePassiveAsync(Options.PaymentProcessedQueue)).ConsumerCount == 1);
        }
        public async Task Stop()
        {
            if (worker is null) return;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await worker.StopAsync(deadline.Token); worker.Dispose(); worker = null;
        }
        public async Task Publish(PaymentProcessedEvent? e, byte[]? body = null)
        {
            await Channel.BasicPublishAsync(Options.PaymentProcessedExchange, "", true,
                new BasicProperties { Persistent = true, ContentType = AdaptadorPaymentProcessedEvent.ContentType,
                    MessageId = TransportId.ToString(), CorrelationId = e?.CorrelationId.ToString(),
                    Headers = new Dictionary<string, object?> { ["teste-header"] = "preservado" } },
                body ?? PaymentTestSupport.Envelope(e!, TransportId));
        }
        public async Task WaitInbox(int count) => await Until(async () =>
        {
            await using var db = Database.Open(); return await db.Inbox.CountAsync() == count && acknowledgments.Events.Count >= count;
        });
        public async Task<BasicGetResult> Error()
        {
            BasicGetResult? result = null;
            await Until(async () => (result = await Channel.BasicGetAsync(Options.PaymentProcessedErrorQueue, true)) is not null);
            return result!;
        }
        public static async Task Until(Func<Task<bool>> condition)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            while (!await condition()) await Task.Delay(50, deadline.Token);
        }
        public async ValueTask DisposeAsync()
        {
            await Stop();
            if (provider is not null) await provider.DisposeAsync();
            if (Channel is not null)
            {
                await Channel.QueueDeleteAsync(Options.PaymentProcessedQueue);
                await Channel.QueueDeleteAsync(Options.PaymentProcessedErrorQueue);
                if (ownsExchange) await Channel.ExchangeDeleteAsync(Options.PaymentProcessedExchange);
                await Channel.DisposeAsync();
            }
            if (connection is not null) await connection.DisposeAsync();
            await Database.DisposeAsync();
        }
    }
}

public sealed class PaymentsPublisherFactAttribute : FactAttribute
{
    public PaymentsPublisherFactAttribute()
    {
        if (new[] { "PAYMENTS_TEST_PUBLISHER", "RABBITMQ_TEST_CONNECTION", "CATALOG_TEST_CONNECTION" }
            .Any(v => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(v))))
            Skip = "Configure PAYMENTS_TEST_PUBLISHER e as conexões de integração para executar o producer externo.";
    }
}
