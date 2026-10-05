using FCG.Catalog.Application.Orders;
namespace FCG.Catalog.Api;

public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, ContextoCorrelacao contexto)
    {
        contexto.Definir(http.Request.Headers["X-Correlation-ID"].Select(x => x ?? "").ToArray());
        http.Response.Headers["X-Correlation-ID"] = contexto.CorrelationId.ToString();
        await next(http);
    }
}
