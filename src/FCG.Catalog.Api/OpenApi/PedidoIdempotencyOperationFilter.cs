using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FCG.Catalog.Api.OpenApi;

public sealed class PedidoIdempotencyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!string.Equals(context.ApiDescription.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(context.ApiDescription.RelativePath?.Trim('/'), "api/v1/pedidos", StringComparison.OrdinalIgnoreCase))
            return;

        var parameter = operation.Parameters?.FirstOrDefault(p =>
            p.In == ParameterLocation.Header
            && string.Equals(p.Name, "Idempotency-Key", StringComparison.OrdinalIgnoreCase));
        if (parameter is null) return;

        parameter.Required = true;
        parameter.Schema ??= new OpenApiSchema();
        parameter.Schema.Type = "string";
        parameter.Schema.Format = "uuid";
        parameter.Example = new OpenApiString(Guid.NewGuid().ToString());
        parameter.Description = "A chave identifica uma intenção de compra. Reutilize a mesma chave em retries, "
            + "inclusive após timeout. Gere outra chave para uma nova tentativa de compra. "
            + "O ID do jogo não é a Idempotency-Key. O exemplo é gerado com o documento OpenAPI, não a cada envio.";
    }
}
