using RebalancePilot.Api.Domain.Enums;

namespace RebalancePilot.Api.Domain.Entities;

public class Position
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string SecurityName { get; set; } = string.Empty;
    public AssetClass AssetClass { get; set; }
    public decimal Shares { get; set; }
    public decimal CostBasisPerShare { get; set; }
    public decimal CurrentMarketPrice { get; set; }

    public decimal TotalValue => Shares * CurrentMarketPrice;
    public decimal UnrealizedGain => (CurrentMarketPrice - CostBasisPerShare) * Shares;
}
