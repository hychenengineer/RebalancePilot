using RebalancePilot.Api.Domain.Enums;

namespace RebalancePilot.Api.Application.DTOs;

public class PositionDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string SecurityName { get; set; } = string.Empty;
    public string AssetClass { get; set; } = string.Empty;
    public decimal Shares { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal TotalValue { get; set; }
    public decimal WeightPct { get; set; }
    public decimal UnrealizedGain { get; set; }
}

public class PortfolioDriftDto
{
    public int AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Custodian { get; set; } = string.Empty;
    public decimal TotalAum { get; set; }
    public decimal CashBalance { get; set; }
    public string TargetModelName { get; set; } = string.Empty;
    
    // Allocations
    public decimal CurrentEquitiesPct { get; set; }
    public decimal TargetEquitiesPct { get; set; }
    public decimal EquitiesDriftPct { get; set; }

    public decimal CurrentFixedIncomePct { get; set; }
    public decimal TargetFixedIncomePct { get; set; }
    public decimal FixedIncomeDriftPct { get; set; }

    public decimal ToleranceBandPct { get; set; }
    public bool IsDriftToleranceExceeded { get; set; }

    public Guid ConcurrencyToken { get; set; }
    public List<PositionDto> Positions { get; set; } = new();
}

public class TradeOrderDto
{
    public string Action { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string SecurityName { get; set; } = string.Empty;
    public string AssetClass { get; set; } = string.Empty;
    public decimal Shares { get; set; }
    public decimal EstimatedPrice { get; set; }
    public decimal EstimatedValue { get; set; }
    public decimal EstimatedCapitalGain { get; set; }
}

public class RebalanceSimulationResultDto
{
    public int ProposalId { get; set; }
    public int AccountId { get; set; }
    public decimal TotalAum { get; set; }
    public decimal TotalTradeVolume { get; set; }
    public decimal TotalEstimatedCapitalGains { get; set; }

    public decimal CurrentEquitiesValue { get; set; }
    public decimal CurrentFixedIncomeValue { get; set; }
    public decimal CurrentEquitiesPct { get; set; }
    public decimal CurrentFixedIncomePct { get; set; }

    public decimal TargetEquitiesPct { get; set; }
    public decimal TargetFixedIncomePct { get; set; }

    public decimal ProjectedEquitiesValue { get; set; }
    public decimal ProjectedFixedIncomeValue { get; set; }
    public decimal ProjectedEquitiesPct { get; set; }
    public decimal ProjectedFixedIncomePct { get; set; }
    public bool IsTargetModelAchieved { get; set; }

    public string Status { get; set; } = string.Empty;
    public string AdvisorNotes { get; set; } = string.Empty;
    public List<TradeOrderDto> Orders { get; set; } = new();
}

public class ApproveTradesResultDto
{
    public int ProposalId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; }
    public int OrdersDispatchedCount { get; set; }
    public string ExecutionSummary { get; set; } = string.Empty;
}

public class AdvisorCoPilotResponseDto
{
    public bool IsValidPrompt { get; set; } = true;
    public string? ValidationMessage { get; set; }
    public string SuggestedAction { get; set; } = string.Empty;
    public decimal MaxCapitalGainsAllowed { get; set; }
    public RebalanceSimulationResultDto? RebalanceProposal { get; set; }
    public string ClientEmailDraft { get; set; } = string.Empty;
}
