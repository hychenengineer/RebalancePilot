using Microsoft.EntityFrameworkCore;
using TamaracCoPilot.Api.Domain.Entities;
using TamaracCoPilot.Api.Infrastructure.Data;

namespace TamaracCoPilot.Api.Infrastructure.Repositories;

public class PortfolioRepository : IPortfolioRepository
{
    private readonly TamaracDbContext _context;

    public PortfolioRepository(TamaracDbContext context)
    {
        _context = context;
    }

    public async Task<List<Account>> GetAllAccountsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Accounts
            .Include(a => a.TargetModel)
            .Include(a => a.Positions)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Account?> GetAccountWithDetailsAsync(int accountId, CancellationToken cancellationToken = default)
    {
        return await _context.Accounts
            .Include(a => a.TargetModel)
            .Include(a => a.Positions)
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
    }

    public async Task<ModelPortfolio?> GetModelPortfolioAsync(int modelId, CancellationToken cancellationToken = default)
    {
        return await _context.ModelPortfolios
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == modelId, cancellationToken);
    }

    public Task UpdateAccountAsync(Account account, CancellationToken cancellationToken = default)
    {
        account.UpdatedAt = DateTime.UtcNow;
        account.ConcurrencyToken = Guid.NewGuid();
        _context.Accounts.Update(account);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
