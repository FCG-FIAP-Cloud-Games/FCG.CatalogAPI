using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FCG.Catalog.Application.Payments;
using FCG.Catalog.Infrastructure.Messaging;
using Xunit;

namespace FCG.Catalog.UnitTests;

public sealed class PaymentTransportTests
{
    private static JsonObject Envelope(string status = "Approved", bool numeric = false)
    {
        var evento = new PaymentProcessedEvent
        {
            EventId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), OccurredAt = DateTimeOffset.UtcNow,
            Version = 1, PaymentId = Guid.NewGuid(), OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Amount = 99.90m, Currency = "BRL", Status = status
        };
        var message = JsonSerializer.SerializeToNode(evento, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        if (!numeric) message["amount"] = "99.90";
        return new JsonObject
        {
            ["messageId"] = Guid.NewGuid().ToString(), ["correlationId"] = Guid.NewGuid().ToString(),
            ["messageType"] = new JsonArray("urn:message:FCG.Payments.Application.Messaging:PaymentProcessedEvent"),
            ["message"] = message
        };
    }
    private static PaymentProcessedEvent Read(JsonObject envelope, string? contentType = AdaptadorPaymentProcessedEvent.ContentType) =>
        AdaptadorPaymentProcessedEvent.Ler(Encoding.UTF8.GetBytes(envelope.ToJsonString()), contentType);

    [Theory]
    [InlineData("Approved", false)]
    [InlineData("Rejected", false)]
    [InlineData("Approved", true)]
    public void Preserva_identificadores_internos_e_decimal(string status, bool numeric)
    {
        var envelope = Envelope(status, numeric);
        var e = Read(envelope);
        Assert.Equal(Guid.Parse(envelope["message"]!["eventId"]!.GetValue<string>()), e.EventId);
        Assert.NotEqual(Guid.Parse(envelope["messageId"]!.GetValue<string>()), e.EventId);
        Assert.Equal(Guid.Parse(envelope["message"]!["correlationId"]!.GetValue<string>()), e.CorrelationId);
        Assert.NotEqual(Guid.Parse(envelope["correlationId"]!.GetValue<string>()), e.CorrelationId);
        Assert.Equal(99.90m, e.Amount);
        Assert.Equal(status, e.Status);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("99,90")]
    [InlineData("1.00000000000000000000000000001")]
    [InlineData("79228162514264337593543950336")]
    [InlineData("99.900000000000000000000000001")]
    [InlineData("")]
    public void Rejeita_decimal_invalido_ou_que_exigiria_arredondamento(string amount)
    {
        var e = Envelope(); e["message"]!["amount"] = amount;
        Assert.Throws<PagamentoInvalidoException>(() => Read(e));
    }

    [Theory]
    [InlineData("eventId")]
    [InlineData("correlationId")]
    [InlineData("occurredAt")]
    [InlineData("version")]
    [InlineData("paymentId")]
    [InlineData("orderId")]
    [InlineData("userId")]
    [InlineData("gameId")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("status")]
    public void Contrato_incompleto_e_permanente(string field)
    {
        var e = Envelope(); e["message"]!.AsObject().Remove(field);
        Assert.Throws<PagamentoInvalidoException>(() => Read(e));
    }

    [Fact]
    public void Rejeita_envelope_ausente_nulo_json_cru_e_malformado()
    {
        var e = Envelope(); e.Remove("message");
        Assert.Throws<PagamentoInvalidoException>(() => Read(e));
        e["message"] = null;
        Assert.Throws<PagamentoInvalidoException>(() => Read(e));
        Assert.Throws<PagamentoInvalidoException>(() => Read(Envelope()["message"]!.AsObject()));
        Assert.Throws<PagamentoInvalidoException>(() =>
            AdaptadorPaymentProcessedEvent.Ler("{"u8, AdaptadorPaymentProcessedEvent.ContentType));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("application/json")]
    [InlineData("text/plain")]
    public void Content_type_desconhecido_nao_e_aceito(string? contentType) =>
        Assert.Throws<PagamentoInvalidoException>(() => Read(Envelope(), contentType));
}
