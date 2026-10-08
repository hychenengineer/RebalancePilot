namespace TamaracCoPilot.Api.Domain.Entities;

public class AuditLogEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string EventType { get; set; } = string.Empty;
    public int? AccountId { get; set; }
    public string Details { get; set; } = string.Empty;
    public string Actor { get; set; } = "Advisor";
}
