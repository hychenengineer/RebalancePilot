using MediatR;
using RebalancePilot.Api.Application.Events;

namespace RebalancePilot.Api.Application.Events.Handlers;

public class AdvisorNotificationEventHandler : INotificationHandler<TradeProposalApprovedEvent>
{
    private readonly ILogger<AdvisorNotificationEventHandler> _logger;

    public AdvisorNotificationEventHandler(ILogger<AdvisorNotificationEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(TradeProposalApprovedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Event: Notification] Push notification sent to Advisor CRM for Account #{AccountId}: Trades executed.",
            notification.AccountId);
        return Task.CompletedTask;
    }
}
