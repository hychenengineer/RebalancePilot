using FluentValidation;
using MediatR;
using RebalancePilot.Api.Application.DTOs;
using RebalancePilot.Api.Application.Services;
using RebalancePilot.Api.Infrastructure.Repositories;

namespace RebalancePilot.Api.Application.Commands;

public record SimulateRebalanceCommand(
    int AccountId,
    decimal? MaxCapitalGainsBudget = null,
    string AdvisorNotes = "",
    List<SpecificTradeInstruction>? CustomTradeInstructions = null,
    List<string>? ExcludedSymbols = null
) : IRequest<RebalanceSimulationResultDto>;

public class SimulateRebalanceCommandValidator : AbstractValidator<SimulateRebalanceCommand>
{
    public SimulateRebalanceCommandValidator()
    {
        RuleFor(x => x.AccountId).GreaterThan(0).WithMessage("AccountId must be greater than 0.");
        RuleFor(x => x.MaxCapitalGainsBudget)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MaxCapitalGainsBudget.HasValue)
            .WithMessage("Capital gains budget cannot be negative.");
    }
}

public class SimulateRebalanceCommandHandler : IRequestHandler<SimulateRebalanceCommand, RebalanceSimulationResultDto>
{
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly ITradeProposalRepository _tradeRepo;
    private readonly IRebalanceService _rebalanceService;

    public SimulateRebalanceCommandHandler(
        IPortfolioRepository portfolioRepo,
        ITradeProposalRepository tradeRepo,
        IRebalanceService rebalanceService)
    {
        _portfolioRepo = portfolioRepo;
        _tradeRepo = tradeRepo;
        _rebalanceService = rebalanceService;
    }

    public async Task<RebalanceSimulationResultDto> Handle(SimulateRebalanceCommand request, CancellationToken cancellationToken)
    {
        var account = await _portfolioRepo.GetAccountWithDetailsAsync(request.AccountId, cancellationToken)
            ?? throw new KeyNotFoundException($"Account #{request.AccountId} not found.");

        var proposal = _rebalanceService.GenerateRebalanceProposal(
            account,
            request.MaxCapitalGainsBudget,
            request.AdvisorNotes,
            request.CustomTradeInstructions,
            request.ExcludedSymbols);

        await _tradeRepo.CreateProposalAsync(proposal, cancellationToken);

        var model = account.TargetModel!;
        var totalAum = account.CalculateTotalPortfolioValue();

        var currentEquitiesValue = account.Positions
            .Where(p => p.AssetClass == Domain.Enums.AssetClass.Equities)
            .Sum(p => p.TotalValue);
        var currentFixedIncomeValue = account.Positions
            .Where(p => p.AssetClass == Domain.Enums.AssetClass.FixedIncome)
            .Sum(p => p.TotalValue);

        var currentEquitiesPct = totalAum > 0 ? Math.Round(currentEquitiesValue / totalAum, 4) : 0m;
        var currentFixedIncomePct = totalAum > 0 ? Math.Round(currentFixedIncomeValue / totalAum, 4) : 0m;

        var equitiesSold = proposal.Orders
            .Where(o => o.Action == Domain.Enums.TradeAction.Sell && o.AssetClass == Domain.Enums.AssetClass.Equities)
            .Sum(o => o.EstimatedValue);
        var bondsBought = proposal.Orders
            .Where(o => o.Action == Domain.Enums.TradeAction.Buy && o.AssetClass == Domain.Enums.AssetClass.FixedIncome)
            .Sum(o => o.EstimatedValue);

        var projectedEquitiesValue = currentEquitiesValue - equitiesSold;
        var projectedFixedIncomeValue = currentFixedIncomeValue + bondsBought;

        var projectedEquitiesPct = totalAum > 0 ? Math.Round(projectedEquitiesValue / totalAum, 4) : 0m;
        var projectedFixedIncomePct = totalAum > 0 ? Math.Round(projectedFixedIncomeValue / totalAum, 4) : 0m;
        var isAchieved = Math.Abs(projectedEquitiesPct - model.TargetEquitiesPct) <= model.ToleranceBandPct;

        return new RebalanceSimulationResultDto
        {
            ProposalId = proposal.Id,
            AccountId = account.Id,
            TotalAum = totalAum,
            TotalTradeVolume = proposal.TotalTradeVolume,
            TotalEstimatedCapitalGains = proposal.TotalEstimatedCapitalGains,
            CurrentEquitiesValue = currentEquitiesValue,
            CurrentFixedIncomeValue = currentFixedIncomeValue,
            CurrentEquitiesPct = currentEquitiesPct,
            CurrentFixedIncomePct = currentFixedIncomePct,
            TargetEquitiesPct = model.TargetEquitiesPct,
            TargetFixedIncomePct = model.TargetFixedIncomePct,
            ProjectedEquitiesValue = projectedEquitiesValue,
            ProjectedFixedIncomeValue = projectedFixedIncomeValue,
            ProjectedEquitiesPct = projectedEquitiesPct,
            ProjectedFixedIncomePct = projectedFixedIncomePct,
            IsTargetModelAchieved = isAchieved,
            Status = proposal.Status.ToString(),
            AdvisorNotes = proposal.AdvisorNotes,
            Orders = proposal.Orders.Select(o => new TradeOrderDto
            {
                Action = o.Action.ToString(),
                Symbol = o.Symbol,
                SecurityName = o.SecurityName,
                AssetClass = o.AssetClass.ToString(),
                Shares = o.Shares,
                EstimatedPrice = o.EstimatedPrice,
                EstimatedValue = o.EstimatedValue,
                EstimatedCapitalGain = o.EstimatedCapitalGain
            }).ToList()
        };
    }
}
