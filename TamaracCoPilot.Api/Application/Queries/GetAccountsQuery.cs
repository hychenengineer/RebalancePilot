using MediatR;
using TamaracCoPilot.Api.Application.DTOs;
using TamaracCoPilot.Api.Infrastructure.Repositories;

namespace TamaracCoPilot.Api.Application.Queries;

public record GetAccountsQuery : IRequest<List<AccountSummaryDto>>;

public class AccountSummaryDto
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Custodian { get; set; } = string.Empty;
    public decimal TotalAum { get; set; }
    public string TargetModelName { get; set; } = string.Empty;
}

public class GetAccountsQueryHandler : IRequestHandler<GetAccountsQuery, List<AccountSummaryDto>>
{
    private readonly IPortfolioRepository _portfolioRepo;

    public GetAccountsQueryHandler(IPortfolioRepository portfolioRepo)
    {
        _portfolioRepo = portfolioRepo;
    }

    public async Task<List<AccountSummaryDto>> Handle(GetAccountsQuery request, CancellationToken cancellationToken)
    {
        var accounts = await _portfolioRepo.GetAllAccountsAsync(cancellationToken);
        return accounts.Select(a => new AccountSummaryDto
        {
            Id = a.Id,
            AccountNumber = a.AccountNumber,
            ClientName = a.ClientName,
            Custodian = a.Custodian,
            TotalAum = a.CalculateTotalPortfolioValue(),
            TargetModelName = a.TargetModel?.ModelName ?? "None"
        }).ToList();
    }
}
