using Microsoft.EntityFrameworkCore;
using TamaracCoPilot.Api.Domain.Entities;
using TamaracCoPilot.Api.Infrastructure.Data;

namespace TamaracCoPilot.Api.Infrastructure.Repositories;

public class TradeProposalRepository : ITradeProposalRepository
{
    private readonly TamaracDbContext _context;

    public TradeProposalRepository(TamaracDbContext context)
    {
        _context = context;
    }

    public async Task<TradeProposal> CreateProposalAsync(TradeProposal proposal, CancellationToken cancellationToken = default)
    {
        await _context.TradeProposals.AddAsync(proposal, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return proposal;
    }

    public async Task<TradeProposal?> GetProposalWithOrdersAsync(int proposalId, CancellationToken cancellationToken = default)
    {
        return await _context.TradeProposals
            .Include(p => p.Orders)
            .FirstOrDefaultAsync(p => p.Id == proposalId, cancellationToken);
    }

    public Task UpdateProposalAsync(TradeProposal proposal, CancellationToken cancellationToken = default)
    {
        _context.TradeProposals.Update(proposal);
        return Task.CompletedTask;
    }

    public async Task AddAuditLogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        await _context.AuditLogs.AddAsync(entry, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
