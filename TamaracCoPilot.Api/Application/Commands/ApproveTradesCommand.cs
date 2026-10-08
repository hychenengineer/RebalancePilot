using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TamaracCoPilot.Api.Application.DTOs;
using TamaracCoPilot.Api.Application.Events;
using TamaracCoPilot.Api.Domain.Enums;
using TamaracCoPilot.Api.Infrastructure.Repositories;

namespace TamaracCoPilot.Api.Application.Commands;

public record ApproveTradesCommand(
    int ProposalId,
    string ApprovedBy,
    Guid? ExpectedConcurrencyToken = null
) : IRequest<ApproveTradesResultDto>;

public class ApproveTradesCommandValidator : AbstractValidator<ApproveTradesCommand>
{
    public ApproveTradesCommandValidator()
    {
        RuleFor(x => x.ProposalId).GreaterThan(0).WithMessage("ProposalId must be greater than 0.");
        RuleFor(x => x.ApprovedBy).NotEmpty().WithMessage("ApprovedBy advisor signature is required.");
    }
}

public class ApproveTradesCommandHandler : IRequestHandler<ApproveTradesCommand, ApproveTradesResultDto>
{
    private readonly ITradeProposalRepository _tradeRepo;
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly IMediator _mediator;

    public ApproveTradesCommandHandler(
        ITradeProposalRepository tradeRepo,
        IPortfolioRepository portfolioRepo,
        IMediator mediator)
    {
        _tradeRepo = tradeRepo;
        _portfolioRepo = portfolioRepo;
        _mediator = mediator;
    }

    public async Task<ApproveTradesResultDto> Handle(ApproveTradesCommand request, CancellationToken cancellationToken)
    {
        var proposal = await _tradeRepo.GetProposalWithOrdersAsync(request.ProposalId, cancellationToken)
            ?? throw new KeyNotFoundException($"Trade Proposal #{request.ProposalId} not found.");

        if (proposal.Status == ProposalStatus.Approved || proposal.Status == ProposalStatus.Dispatched)
        {
            throw new InvalidOperationException($"Proposal #{request.ProposalId} has already been approved.");
        }

        var account = await _portfolioRepo.GetAccountWithDetailsAsync(proposal.AccountId, cancellationToken)
            ?? throw new KeyNotFoundException($"Account #{proposal.AccountId} not found.");

        // Optimistic Concurrency Check
        if (request.ExpectedConcurrencyToken.HasValue && 
            account.ConcurrencyToken != request.ExpectedConcurrencyToken.Value)
        {
            throw new DbUpdateConcurrencyException("The account was modified by another transaction or advisor. Please re-run drift simulation.");
        }

        proposal.Status = ProposalStatus.Approved;
        proposal.ApprovedBy = request.ApprovedBy;
        proposal.ApprovedAt = DateTime.UtcNow;

        await _tradeRepo.UpdateProposalAsync(proposal, cancellationToken);
        await _portfolioRepo.UpdateAccountAsync(account, cancellationToken);

        await _tradeRepo.SaveChangesAsync(cancellationToken);
        await _portfolioRepo.SaveChangesAsync(cancellationToken);

        // Publish Event-Driven Notification across subscribers!
        await _mediator.Publish(new TradeProposalApprovedEvent(
            proposal.Id,
            account.Id,
            request.ApprovedBy,
            proposal.Orders.ToList(),
            DateTime.UtcNow
        ), cancellationToken);

        return new ApproveTradesResultDto
        {
            ProposalId = proposal.Id,
            Status = proposal.Status.ToString(),
            ApprovedBy = proposal.ApprovedBy,
            ApprovedAt = proposal.ApprovedAt.Value,
            OrdersDispatchedCount = proposal.Orders.Count,
            ExecutionSummary = $"Successfully approved and dispatched {proposal.Orders.Count} orders to custodian queue with audit stamp."
        };
    }
}
