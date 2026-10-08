using MediatR;
using Microsoft.AspNetCore.Mvc;
using RebalancePilot.Api.Application.DTOs;
using RebalancePilot.Api.Application.Queries;

namespace RebalancePilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PortfoliosController : ControllerBase
{
    private readonly IMediator _mediator;

    public PortfoliosController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Retrieves all client accounts under management.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<AccountSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccounts(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAccountsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Calculates portfolio asset allocation and evaluates drift against the assigned model.
    /// </summary>
    [HttpGet("{id:int}/drift")]
    [ProducesResponseType(typeof(PortfolioDriftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDrift(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPortfolioDriftQuery(id), cancellationToken);
        if (result == null)
        {
            return NotFound(new { message = $"Account #{id} was not found." });
        }

        return Ok(result);
    }
}
