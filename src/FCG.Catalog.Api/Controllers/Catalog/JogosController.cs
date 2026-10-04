using Microsoft.AspNetCore.Authorization;
using FCG.Catalog.Api.Contracts.Catalog.Jogos;
using FCG.Catalog.Application.Catalog.Jogos;
using Microsoft.AspNetCore.Mvc;

namespace FCG.Catalog.Api.Controllers.Catalog;

[ApiController]
[Route("api/v1/jogos")]
[Tags("Jogos")]
public sealed class JogosController : ControllerBase
{
    private const int PaginaPadrao = 1;
    private const int TamanhoPaginaPadrao = 20;

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<RespostaJogo>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RespostaJogo>> CriarAsync(
        RequisicaoCriarJogo requisicao,
        ManipuladorCriarJogo manipulador,
        CancellationToken tokenCancelamento)
    {
        var resultado = await manipulador.ProcessarAsync(
            new ComandoCriarJogo(
                requisicao.Titulo,
                requisicao.Descricao,
                requisicao.FaixaEtaria,
                requisicao.Preco),
            tokenCancelamento);

        if (resultado.Status == StatusCriacaoJogo.DadosInvalidos)
        {
            return BadRequest(new ValidationProblemDetails(resultado.Erros.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Um ou mais dados informados são inválidos."
            });
        }

        var jogo = resultado.Jogo!;
        var resposta = ParaResposta(jogo);

        return Created($"/api/v1/jogos/{jogo.Id}", resposta);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<RespostaJogo>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RespostaJogo>> ObterPorIdAsync(
        Guid id,
        ManipuladorObterJogoPorId manipulador,
        CancellationToken tokenCancelamento)
    {
        var resultado = await manipulador.ProcessarAsync(
            new ConsultaObterJogoPorId(id),
            tokenCancelamento);

        if (resultado.Status == StatusConsultaJogo.IdInvalido)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Identificador inválido.",
                Detail = "O identificador do jogo informado é inválido."
            });
        }

        if (resultado.Status == StatusConsultaJogo.NaoEncontrado)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Jogo não encontrado.",
                Detail = "Não existe jogo cadastrado com o identificador informado."
            });
        }

        return Ok(ParaResposta(resultado.Jogo!));
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<RespostaListaJogos>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RespostaListaJogos>> ListarAsync(
        ManipuladorListarJogos manipulador,
        CancellationToken tokenCancelamento,
        [FromQuery] int pagina = PaginaPadrao,
        [FromQuery] int tamanhoPagina = TamanhoPaginaPadrao)
    {
        var resultado = await manipulador.ProcessarAsync(
            new ConsultaListarJogos(pagina, tamanhoPagina),
            tokenCancelamento);

        if (resultado.Status == StatusListagemJogos.PaginacaoInvalida)
        {
            return BadRequest(new ValidationProblemDetails(resultado.Erros.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Um ou mais dados informados são inválidos."
            });
        }

        var resposta = new RespostaListaJogos(
            resultado.Itens.Select(ParaResposta).ToList(),
            resultado.Pagina,
            resultado.TamanhoPagina);

        return Ok(resposta);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<RespostaJogo>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RespostaJogo>> AtualizarAsync(
        Guid id,
        RequisicaoAtualizarJogo requisicao,
        ManipuladorAtualizarJogo manipulador,
        CancellationToken tokenCancelamento)
    {
        var resultado = await manipulador.ProcessarAsync(
            new ComandoAtualizarJogo(
                id,
                requisicao.Titulo,
                requisicao.Descricao,
                requisicao.FaixaEtaria,
                requisicao.Preco),
            tokenCancelamento);

        if (resultado.Status == StatusAtualizacaoJogo.DadosInvalidos)
        {
            return BadRequest(new ValidationProblemDetails(resultado.Erros.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Um ou mais dados informados são inválidos."
            });
        }

        if (resultado.Status == StatusAtualizacaoJogo.NaoEncontrado)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Jogo não encontrado.",
                Detail = "Não existe jogo cadastrado com o identificador informado."
            });
        }

        return Ok(ParaResposta(resultado.Jogo!));
    }

    private static RespostaJogo ParaResposta(JogoObtido jogo) =>
        new(
            jogo.Id,
            jogo.Titulo,
            jogo.Descricao,
            jogo.FaixaEtaria,
            jogo.Preco,
            jogo.Ativo,
            jogo.DataCadastro);

    private static RespostaJogo ParaResposta(JogoCriado jogo) =>
        new(
            jogo.Id,
            jogo.Titulo,
            jogo.Descricao,
            jogo.FaixaEtaria,
            jogo.Preco,
            jogo.Ativo,
            jogo.DataCadastro);
}
