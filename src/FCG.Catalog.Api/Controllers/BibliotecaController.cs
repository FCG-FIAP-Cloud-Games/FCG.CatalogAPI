using FCG.Catalog.Application.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FCG.Catalog.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/biblioteca")]
public sealed class BibliotecaController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ItemBiblioteca>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Listar([FromServices] IConsultaListaBiblioteca consulta, CancellationToken ct) =>
        Ok(await consulta.ListarAsync(Guid.Parse(User.FindFirst("sub")!.Value), ct));
}
