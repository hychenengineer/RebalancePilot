using RebalancePilot.Api.Domain.Enums;

namespace RebalancePilot.Api.Domain.Contracts;

public enum RebalanceStrategyMode
{
    FullModelRebalance,
    CustomTradesOnly,
    RaiseCash
}

public enum TradeAmountType
{
    Shares,
    Percent,
    Dollars
}

public record IntentTradeOrder(
    TradeAction Action,
    string Symbol,
    TradeAmountType AmountType,
    decimal Amount
);

public class AdvisorRebalanceIntent
{
    public RebalanceStrategyMode StrategyMode { get; set; } = RebalanceStrategyMode.FullModelRebalance;
    public decimal? MaxCapitalGainsBudget { get; set; }
    public bool DisregardTaxBudget { get; set; }
    public List<string> ExcludedSymbols { get; set; } = new();
    public List<string> ExcludedSectorsOrThemes { get; set; } = new();
    public List<IntentTradeOrder> SpecificTrades { get; set; } = new();
    public string SummaryRationale { get; set; } = string.Empty;
}
