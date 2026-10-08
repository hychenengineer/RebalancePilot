using MediatR;
using RebalancePilot.Api.Domain.Entities;

namespace RebalancePilot.Api.Application.Events;

public record PortfolioDriftDetectedEvent(
    int AccountId,
    string ClientName,
    decimal EquitiesDriftPct,
    DateTime Timestamp
) : INotification;

public record TradeProposalApprovedEvent(
    int ProposalId,
    int AccountId,
    string ApprovedBy,
    List<TradeOrder> Orders,
    DateTime Timestamp
) : INotification;
