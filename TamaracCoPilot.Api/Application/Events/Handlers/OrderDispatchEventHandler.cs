using MediatR;
using TamaracCoPilot.Api.Application.Events;

namespace TamaracCoPilot.Api.Application.Events.Handlers;

public class OrderDispatchEventHandler : INotificationHandler<TradeProposalApprovedEvent>
{
    private readonly ILogger<OrderDispatchEventHandler> _logger;

    public OrderDispatchEventHandler(ILogger<OrderDispatchEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(TradeProposalApprovedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Event: OMS Dispatch] Simulating downstream order routing to Custodian Trading Desk / Kafka...");
        
        foreach (var order in notification.Orders)
        {
            _logger.LogInformation("  -> Dispatched FIX 4.4 Order: {Action} {Shares} {Symbol} @ est. ${Price:F2}",
                order.Action, order.Shares, order.Symbol, order.EstimatedPrice);
        }

        _logger.LogInformation("[Event: OMS Dispatch] {Count} orders successfully enqueued for market execution.", notification.Orders.Count);
        return Task.CompletedTask;
    }
}
