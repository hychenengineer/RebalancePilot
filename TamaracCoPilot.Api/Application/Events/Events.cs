using MediatR;
using TamaracCoPilot.Api.Domain.Entities;

namespace TamaracCoPilot.Api.Application.Events;

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
