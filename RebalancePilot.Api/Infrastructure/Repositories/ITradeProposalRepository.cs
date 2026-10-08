using RebalancePilot.Api.Domain.Entities;

namespace RebalancePilot.Api.Infrastructure.Repositories;

public interface ITradeProposalRepository
{
    Task<TradeProposal> CreateProposalAsync(TradeProposal proposal, CancellationToken cancellationToken = default);
    Task<TradeProposal?> GetProposalWithOrdersAsync(int proposalId, CancellationToken cancellationToken = default);
    Task UpdateProposalAsync(TradeProposal proposal, CancellationToken cancellationToken = default);
    Task AddAuditLogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
