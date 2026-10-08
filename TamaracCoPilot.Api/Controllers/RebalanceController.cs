using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TamaracCoPilot.Api.Application.Commands;
using TamaracCoPilot.Api.Application.DTOs;

namespace TamaracCoPilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RebalanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public RebalanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Simulates a tax-aware rebalancing proposal without executing trades.
    /// </summary>
    [HttpPost("simulate")]
    [ProducesResponseType(typeof(RebalanceSimulationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Simulate([FromBody] SimulateRebalanceCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors.Select(e => e.ErrorMessage) });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Fiduciary Human-in-the-Loop approval gate: signs off on trade proposal and dispatches orders to custodian.
    /// </summary>
    [HttpPost("approve")]
    [ProducesResponseType(typeof(ApproveTradesResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve([FromBody] ApproveTradesCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors.Select(e => e.ErrorMessage) });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Advisor Co-Pilot: interprets natural language rebalance instructions and drafts client correspondence.
    /// </summary>
    [HttpPost("copilot-ask")]
    [ProducesResponseType(typeof(AdvisorCoPilotResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CoPilotAsk([FromBody] AdvisorCoPilotAskCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors.Select(e => e.ErrorMessage) });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
