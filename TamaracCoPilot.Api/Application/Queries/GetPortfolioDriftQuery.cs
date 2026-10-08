using MediatR;
using TamaracCoPilot.Api.Application.DTOs;
using TamaracCoPilot.Api.Application.Events;
using TamaracCoPilot.Api.Application.Services;
using TamaracCoPilot.Api.Infrastructure.Repositories;

namespace TamaracCoPilot.Api.Application.Queries;

public record GetPortfolioDriftQuery(int AccountId) : IRequest<PortfolioDriftDto?>;

public class GetPortfolioDriftQueryHandler : IRequestHandler<GetPortfolioDriftQuery, PortfolioDriftDto?>
{
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly IRebalanceService _rebalanceService;
    private readonly IMediator _mediator;

    public GetPortfolioDriftQueryHandler(
        IPortfolioRepository portfolioRepo,
        IRebalanceService rebalanceService,
        IMediator mediator)
    {
        _portfolioRepo = portfolioRepo;
        _rebalanceService = rebalanceService;
        _mediator = mediator;
    }

    public async Task<PortfolioDriftDto?> Handle(GetPortfolioDriftQuery request, CancellationToken cancellationToken)
    {
        var account = await _portfolioRepo.GetAccountWithDetailsAsync(request.AccountId, cancellationToken);
        if (account == null) return null;

        var drift = _rebalanceService.CalculateDrift(account);

        // If drift exceeds model tolerance, publish domain event!
        if (drift.IsDriftToleranceExceeded)
        {
            await _mediator.Publish(new PortfolioDriftDetectedEvent(
                account.Id,
                account.ClientName,
                drift.EquitiesDriftPct,
                DateTime.UtcNow
            ), cancellationToken);
        }

        return drift;
    }
}
