using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FCG.Catalog.Application.Payments;

namespace FCG.Catalog.Infrastructure.Messaging;

public static class AdaptadorPaymentProcessedEvent
{
    public const string ContentType = "application/vnd.masstransit+json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new DecimalExatoConverter() }
    };

    public static PaymentProcessedEvent Ler(ReadOnlySpan<byte> body, string? contentType)
    {
        if (!string.Equals(contentType?.Split(';')[0].Trim(), ContentType, StringComparison.OrdinalIgnoreCase))
            throw new PagamentoInvalidoException("ContentTypeNaoSuportado");
        try
        {
            var envelope = JsonSerializer.Deserialize<EnvelopeMassTransit>(body, JsonOptions);
            var message = envelope?.Message ?? throw new PagamentoInvalidoException("EnvelopeSemMessage");
            ValidadorPaymentProcessedEvent.Validar(message);
            return message;
        }
        catch (JsonException) { throw new PagamentoInvalidoException("JsonOuContratoInvalido"); }
    }

    // Metadados do envelope nunca substituem os identificadores contratuais internos.
    private sealed class EnvelopeMassTransit
    {
        public PaymentProcessedEvent? Message { get; init; }
    }

    private sealed class DecimalExatoConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var text = reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => Encoding.UTF8.GetString(reader.ValueSpan),
                _ => null
            };
            // Gramática decimal invariável. Rejeita expoentes, separadores locais e valores
            // que decimal.TryParse arredondaria silenciosamente além da sua precisão.
            if (string.IsNullOrEmpty(text)) throw new JsonException();
            var digits = text[0] == '-' ? text[1..] : text;
            var parts = digits.Split('.');
            if (parts.Length > 2 || parts.Any(p => p.Length == 0 || p.Any(c => c is < '0' or > '9')))
                throw new JsonException();
            var scale = parts.Length == 2 ? parts[1].TrimEnd('0').Length : 0;
            if (scale > 28) throw new JsonException();
            if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var amount)) throw new JsonException();
            var normalized = digits.TrimStart('0');
            if (normalized.Length == 0 || normalized[0] == '.') normalized = "0" + normalized;
            if (normalized.Contains('.')) normalized = normalized.TrimEnd('0').TrimEnd('.');
            var actual = decimal.Abs(amount).ToString("0.############################", CultureInfo.InvariantCulture);
            if (actual != normalized) throw new JsonException();
            return amount;
        }
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value);
    }
}
