using TamaracCoPilot.Api.Domain.Entities;

namespace TamaracCoPilot.Api.Infrastructure.Repositories;

public interface IPortfolioRepository
{
    Task<List<Account>> GetAllAccountsAsync(CancellationToken cancellationToken = default);
    Task<Account?> GetAccountWithDetailsAsync(int accountId, CancellationToken cancellationToken = default);
    Task<ModelPortfolio?> GetModelPortfolioAsync(int modelId, CancellationToken cancellationToken = default);
    Task UpdateAccountAsync(Account account, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
