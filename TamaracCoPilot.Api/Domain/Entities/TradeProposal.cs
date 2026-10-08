using TamaracCoPilot.Api.Domain.Enums;

namespace TamaracCoPilot.Api.Domain.Entities;

public class TradeProposal
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public ProposalStatus Status { get; set; } = ProposalStatus.Draft;
    public string AdvisorNotes { get; set; } = string.Empty;
    public decimal TotalEstimatedCapitalGains { get; set; }
    public decimal TotalTradeVolume { get; set; }
    public List<TradeOrder> Orders { get; set; } = new();
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
