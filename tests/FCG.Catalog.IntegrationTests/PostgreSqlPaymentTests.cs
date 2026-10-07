using System.Net;
using FCG.Catalog.Application.Abstractions.Repositories;
using FCG.Catalog.Application.Orders;
using FCG.Catalog.Application.Payments;
using FCG.Catalog.Domain.Library;
using FCG.Catalog.Domain.Orders;
using FCG.Catalog.Infrastructure.Data.EF;
using FCG.Catalog.Infrastructure.Data.EF.Context;
using FCG.Catalog.Infrastructure.Inbox;
using FCG.Catalog.Infrastructure.Repositories.Catalog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace FCG.Catalog.IntegrationTests;

public sealed class PostgreSqlPaymentTests
{
    [PostgreSqlFact]
    public async Task Approved_rejected_repeticoes_contradicoes_e_inbox_unique()
    {
        await using var env = new BibliotecaDatabase(); await env.InitializeAsync();
        await using var seed = env.Open();
        foreach (var status in new[] { "Approved", "Rejected" })
        {
            var p = await PostgreSqlBibliotecaTests.Seed(seed); var e = PaymentTestSupport.Event(p, status);
            await using var db = env.Open();
            Assert.Equal(ResultadoPagamento.Processado, await PaymentTestSupport.Handler(db).ExecutarAsync(e));
            var stored = await db.Pedidos.AsNoTracking().SingleAsync(x => x.Id == p.Id);
            Assert.Equal(status == "Approved" ? StatusPedido.Paid : StatusPedido.Rejected, stored.Status);
            var date = stored.UpdatedAt;
            Assert.Equal(ResultadoPagamento.Duplicado, await PaymentTestSupport.Handler(db).ExecutarAsync(e));
            Assert.Equal(ResultadoPagamento.Compativel, await PaymentTestSupport.Handler(db).ExecutarAsync(e with { EventId = Guid.NewGuid() }));
            await Assert.ThrowsAsync<PagamentoInvalidoException>(() => PaymentTestSupport.Handler(db).ExecutarAsync(e with
                { EventId = Guid.NewGuid(), Status = status == "Approved" ? "Rejected" : "Approved" }));
            Assert.Equal(date, (await db.Pedidos.AsNoTracking().SingleAsync(x => x.Id == p.Id)).UpdatedAt);
            var owned = await db.Aquisicoes.Where(a => a.UsuarioId == p.UserId).ToListAsync();
            if (status == "Approved") Assert.Equal(p.Id, Assert.Single(owned).PedidoId);
            else Assert.Empty(owned);
        }
        Assert.Equal(4, await seed.Inbox.CountAsync());
        var first = await seed.Inbox.AsNoTracking().FirstAsync();
        seed.Inbox.Add(new MensagemInbox(first.ConsumerName, first.EventId));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => seed.SaveChangesAsync());
        Assert.Equal("ux_inbox_consumer_event", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
        seed.ChangeTracker.Clear();
        seed.Inbox.Add(new MensagemInbox("Outro.Consumer", first.EventId));
        await seed.SaveChangesAsync(); // A unicidade inclui ConsumerName.
    }

    [PostgreSqlFact]
    public async Task Rollback_antes_commit_sem_efeitos_parciais_redelivery_conclui()
    {
        await using var env = new BibliotecaDatabase(); await env.InitializeAsync();
        await using var seed = env.Open(); var p = await PostgreSqlBibliotecaTests.Seed(seed);
        var e = PaymentTestSupport.Event(p); var gate = new BeforeCommit { Fail = true };
        await using var db = PaymentTestSupport.Open(env.ConnectionString, gate);
        var task = PaymentTestSupport.Handler(db).ExecutarAsync(e);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await using var observer = env.Open();
        Assert.Equal(StatusPedido.PendingPayment, (await observer.Pedidos.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(await observer.Aquisicoes.ToListAsync()); Assert.Empty(await observer.Inbox.ToListAsync());
        gate.Release.TrySetResult();
        await Assert.ThrowsAsync<TimeoutException>(() => task);
        Assert.Equal(StatusPedido.PendingPayment, (await observer.Pedidos.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(await observer.Aquisicoes.ToListAsync()); Assert.Empty(await observer.Inbox.ToListAsync());
        await PaymentTestSupport.Handler(observer).ExecutarAsync(e);
        Assert.Equal(StatusPedido.Paid, (await observer.Pedidos.SingleAsync()).Status);
        Assert.Single(await observer.Aquisicoes.ToListAsync()); Assert.Single(await observer.Inbox.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task Concurrentes_mesmo_evento_e_eventos_distintos_nao_duplicam_posse()
    {
        await using var env = new BibliotecaDatabase(); await env.InitializeAsync();
        await using var seed = env.Open(); var p = await PostgreSqlBibliotecaTests.Seed(seed); var e = PaymentTestSupport.Event(p);
        async Task<ResultadoPagamento> Run(PaymentProcessedEvent message)
        { await using var db = env.Open(); return await PaymentTestSupport.Handler(db).ExecutarAsync(message); }
        var results = await Task.WhenAll(Run(e), Run(e), Run(e with { EventId = Guid.NewGuid() }));
        Assert.Single(results, r => r == ResultadoPagamento.Processado);
        Assert.Single(results, r => r == ResultadoPagamento.Duplicado);
        Assert.Single(results, r => r == ResultadoPagamento.Compativel);
        Assert.Equal(2, await seed.Inbox.CountAsync()); Assert.Single(await seed.Aquisicoes.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task Posse_historica_valida_e_inconsistencia_de_pedido_nao_mascarada()
    {
        await using var env = new BibliotecaDatabase(); await env.InitializeAsync();
        await using var db = env.Open(); var p = await PostgreSqlBibliotecaTests.Seed(db);
        var history = Aquisicao.Historica(Guid.NewGuid(), p.UserId, p.GameId, DateTimeOffset.UtcNow);
        db.Aquisicoes.Add(history); await db.SaveChangesAsync();
        await PaymentTestSupport.Handler(db).ExecutarAsync(PaymentTestSupport.Event(p));
        Assert.Null((await db.Aquisicoes.SingleAsync()).PedidoId);
        var bad = await PostgreSqlBibliotecaTests.Seed(db);
        db.Aquisicoes.Add(Aquisicao.Conceder(Guid.NewGuid(), bad.GameId, bad.Id)); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<PagamentoInvalidoException>(() => PaymentTestSupport.Handler(db).ExecutarAsync(PaymentTestSupport.Event(bad)));
        Assert.Equal(StatusPedido.PendingPayment, (await db.Pedidos.SingleAsync(x => x.Id == bad.Id)).Status);
        Assert.Single(await db.Inbox.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task Approved_segura_lock_compartilhado_com_POST_ate_commit()
    {
        await using var env = new BibliotecaDatabase(); await env.InitializeAsync();
        await using var seed = env.Open(); var p = await PostgreSqlBibliotecaTests.Seed(seed);
        var commit = new BeforeCommit();
        await using var paymentDb = PaymentTestSupport.Open(env.ConnectionString, commit);
        var payment = PaymentTestSupport.Handler(paymentDb).ExecutarAsync(PaymentTestSupport.Event(p));
        await commit.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var tokens = new CatalogFactory();
        using var host = tokens.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:CatalogDatabase", env.ConnectionString);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRepositorioJogos>(); services.Add(PaymentTestSupport.JogosDescriptor);
                services.RemoveAll<ILockUsuarioJogo>();
                services.AddScoped<ILockUsuarioJogo>(s => new ObservedLock(new LockUsuarioJogo(s.GetRequiredService<CatalogDbContext>()), attempted));
            });
        });
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.Token(role: "Usuario", userId: p.UserId));
        var post = PedidosApiTests.Post(client, p.GameId, Guid.NewGuid().ToString());
        try
        {
            await attempted.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(post.IsCompleted);
        }
        finally { commit.Release.TrySetResult(); }
        await payment;
        using var response = await post.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(await seed.Pedidos.ToListAsync()); Assert.Single(await seed.Aquisicoes.ToListAsync());
        Assert.Empty(await seed.Outbox.ToListAsync());
    }

    internal sealed class ObservedLock(ILockUsuarioJogo inner, TaskCompletionSource attempted) : ILockUsuarioJogo
    {
        public Task AcquireAsync(Guid u, Guid g, CancellationToken cancellationToken = default)
        { attempted.TrySetResult(); return inner.AcquireAsync(u, g, cancellationToken); }
    }
}
