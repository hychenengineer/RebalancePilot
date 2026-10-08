using System.ComponentModel.DataAnnotations;

namespace RebalancePilot.Api.Domain.Entities;

public class Account
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Custodian { get; set; } = "Schwab"; // e.g. Fidelity, Schwab, Pershing
    public decimal CashBalance { get; set; }
    
    public int TargetModelId { get; set; }
    public ModelPortfolio? TargetModel { get; set; }
    
    public List<Position> Positions { get; set; } = new();

    [ConcurrencyCheck]
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public decimal CalculateTotalPortfolioValue()
    {
        return CashBalance + Positions.Sum(p => p.TotalValue);
    }
}
