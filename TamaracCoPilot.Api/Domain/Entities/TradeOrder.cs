using TamaracCoPilot.Api.Domain.Enums;

namespace TamaracCoPilot.Api.Domain.Entities;

public class TradeOrder
{
    public int Id { get; set; }
    public int TradeProposalId { get; set; }
    public TradeAction Action { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string SecurityName { get; set; } = string.Empty;
    public AssetClass AssetClass { get; set; }
    public decimal Shares { get; set; }
    public decimal EstimatedPrice { get; set; }
    public decimal EstimatedValue => Shares * EstimatedPrice;
    public decimal EstimatedCapitalGain { get; set; }
}
