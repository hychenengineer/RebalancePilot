using MediatR;
using RebalancePilot.Api.Application.Events;
using RebalancePilot.Api.Domain.Entities;
using RebalancePilot.Api.Infrastructure.Repositories;

namespace RebalancePilot.Api.Application.Events.Handlers;

public class AuditTrailEventHandler : 
    INotificationHandler<PortfolioDriftDetectedEvent>,
    INotificationHandler<TradeProposalApprovedEvent>
{
    private readonly ITradeProposalRepository _tradeRepo;
    private readonly ILogger<AuditTrailEventHandler> _logger;

    public AuditTrailEventHandler(ITradeProposalRepository tradeRepo, ILogger<AuditTrailEventHandler> logger)
    {
        _tradeRepo = tradeRepo;
        _logger = logger;
    }

    public async Task Handle(PortfolioDriftDetectedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Event: Audit] Logging portfolio drift for Account {AccountId} ({ClientName}): {Drift:P2}",
            notification.AccountId, notification.ClientName, notification.EquitiesDriftPct);

        var audit = new AuditLogEntry
        {
            Timestamp = notification.Timestamp,
            EventType = "PORTFOLIO_DRIFT_EXCEEDED",
            AccountId = notification.AccountId,
            Actor = "RebalancePilotDriftEngine",
            Details = $"Equities drift measured at {notification.EquitiesDriftPct:P2} exceeding model tolerance."
        };

        await _tradeRepo.AddAuditLogAsync(audit, cancellationToken);
        await _tradeRepo.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(TradeProposalApprovedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Event: Audit] Logging trade approval for Proposal #{ProposalId} by {ApprovedBy}",
            notification.ProposalId, notification.ApprovedBy);

        var audit = new AuditLogEntry
        {
            Timestamp = notification.Timestamp,
            EventType = "TRADE_PROPOSAL_APPROVED",
            AccountId = notification.AccountId,
            Actor = notification.ApprovedBy,
            Details = $"Approved proposal #{notification.ProposalId} containing {notification.Orders.Count} orders."
        };

        await _tradeRepo.AddAuditLogAsync(audit, cancellationToken);
        await _tradeRepo.SaveChangesAsync(cancellationToken);
    }
}
