using TamaracCoPilot.Api.Domain.Enums;

namespace TamaracCoPilot.Api.Domain.Entities;

public class ModelPortfolio
{
    public int Id { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal TargetEquitiesPct { get; set; }
    public decimal TargetFixedIncomePct { get; set; }
    public decimal TargetCashPct { get; set; }
    public decimal ToleranceBandPct { get; set; } = 0.05m; // 5% drift threshold
}
