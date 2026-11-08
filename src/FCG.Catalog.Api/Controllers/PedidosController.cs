using FCG.Catalog.Application.Orders;
using FCG.Catalog.Domain.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FCG.Catalog.Api.Controllers;

public sealed record CriarPedidoRequest(Guid JogoId);
public sealed record RespostaPedido(Guid OrderId, Guid GameId, decimal Price, string Currency,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static RespostaPedido De(Pedido p) => new(p.Id, p.GameId, p.Price, p.Currency, p.Status.ToString(), p.CreatedAt, p.UpdatedAt);
}

[ApiController]
[Authorize]
[Route("api/v1/pedidos")]
public sealed class PedidosController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(RespostaPedido), 202)]
    [ProducesResponseType(typeof(RespostaPedido), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> Criar(CriarPedidoRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || !Guid.TryParse(idempotencyKey, out var key) || key == Guid.Empty || request.JogoId == Guid.Empty)
            return Problem(statusCode: 400, detail: "Informe jogoId e um único Idempotency-Key UUID não vazio.");
        var handler = HttpContext.RequestServices.GetRequiredService<ManipuladorCriarPedido>();
        var result = await handler.ExecutarAsync(Guid.Parse(User.FindFirst("sub")!.Value), request.JogoId, key, ct);
        if (result.Pedido is null) return Problem(statusCode: result.Codigo, detail: result.Erro);
        var response = RespostaPedido.De(result.Pedido);
        return result.Codigo == 202 ? Accepted($"/api/v1/pedidos/{response.OrderId}", response) : Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RespostaPedido), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Obter(Guid id, [FromServices] IRepositorioPedidos repository, CancellationToken ct)
    {
        var pedido = await repository.ObterPorIdAsync(id, ct);
        if (pedido is null) return NotFound();
        if (pedido.UserId != Guid.Parse(User.FindFirst("sub")!.Value) && !User.IsInRole("Administrador"))
            return Forbid();
        return Ok(RespostaPedido.De(pedido));
    }
}
