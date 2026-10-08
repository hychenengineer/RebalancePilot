using TamaracCoPilot.Api.Application.DTOs;
using TamaracCoPilot.Api.Domain.Entities;
using TamaracCoPilot.Api.Domain.Enums;

using TamaracCoPilot.Api.Domain.Contracts;

namespace TamaracCoPilot.Api.Application.Services;

public interface IRebalanceService
{
    PortfolioDriftDto CalculateDrift(Account account);
    TradeProposal GenerateRebalanceProposal(
        Account account, 
        decimal? maxCapitalGainsBudget = null, 
        string advisorNotes = "",
        List<SpecificTradeInstruction>? customTradeInstructions = null,
        List<string>? excludedSymbols = null);
}

public class RebalanceService : IRebalanceService
{
    public PortfolioDriftDto CalculateDrift(Account account)
    {
        var model = account.TargetModel ?? throw new InvalidOperationException("Account target model is missing.");
        var totalAum = account.CalculateTotalPortfolioValue();

        if (totalAum <= 0)
        {
            throw new InvalidOperationException("Account has zero or negative total AUM.");
        }

        var equitiesValue = account.Positions
            .Where(p => p.AssetClass == AssetClass.Equities)
            .Sum(p => p.TotalValue);

        var fixedIncomeValue = account.Positions
            .Where(p => p.AssetClass == AssetClass.FixedIncome)
            .Sum(p => p.TotalValue);

        var currentEquitiesPct = Math.Round(equitiesValue / totalAum, 4);
        var currentFixedIncomePct = Math.Round(fixedIncomeValue / totalAum, 4);

        var equitiesDrift = currentEquitiesPct - model.TargetEquitiesPct;
        var fixedIncomeDrift = currentFixedIncomePct - model.TargetFixedIncomePct;

        var isDriftExceeded = Math.Abs(equitiesDrift) > model.ToleranceBandPct ||
                              Math.Abs(fixedIncomeDrift) > model.ToleranceBandPct;

        return new PortfolioDriftDto
        {
            AccountId = account.Id,
            AccountNumber = account.AccountNumber,
            ClientName = account.ClientName,
            Custodian = account.Custodian,
            TotalAum = totalAum,
            CashBalance = account.CashBalance,
            TargetModelName = model.ModelName,
            CurrentEquitiesPct = currentEquitiesPct,
            TargetEquitiesPct = model.TargetEquitiesPct,
            EquitiesDriftPct = Math.Round(equitiesDrift, 4),
            CurrentFixedIncomePct = currentFixedIncomePct,
            TargetFixedIncomePct = model.TargetFixedIncomePct,
            FixedIncomeDriftPct = Math.Round(fixedIncomeDrift, 4),
            ToleranceBandPct = model.ToleranceBandPct,
            IsDriftToleranceExceeded = isDriftExceeded,
            ConcurrencyToken = account.ConcurrencyToken,
            Positions = account.Positions.Select(p => new PositionDto
            {
                Id = p.Id,
                Symbol = p.Symbol,
                SecurityName = p.SecurityName,
                AssetClass = p.AssetClass.ToString(),
                Shares = p.Shares,
                CurrentPrice = p.CurrentMarketPrice,
                TotalValue = p.TotalValue,
                WeightPct = Math.Round(p.TotalValue / totalAum, 4),
                UnrealizedGain = p.UnrealizedGain
            }).ToList()
        };
    }

    public TradeProposal GenerateRebalanceProposal(
        Account account, 
        decimal? maxCapitalGainsBudget = null, 
        string advisorNotes = "",
        List<SpecificTradeInstruction>? customTradeInstructions = null,
        List<string>? excludedSymbols = null)
    {
        var drift = CalculateDrift(account);
        var model = account.TargetModel!;
        var totalAum = drift.TotalAum;

        var targetEquitiesValue = totalAum * model.TargetEquitiesPct;
        var currentEquitiesValue = account.Positions
            .Where(p => p.AssetClass == AssetClass.Equities)
            .Sum(p => p.TotalValue);

        var equitiesExcessValue = currentEquitiesValue - targetEquitiesValue;

        var proposal = new TradeProposal
        {
            AccountId = account.Id,
            CreatedDate = DateTime.UtcNow,
            Status = ProposalStatus.AwaitingSignOff,
            AdvisorNotes = string.IsNullOrWhiteSpace(advisorNotes) ? "Automated rebalance proposal." : advisorNotes,
            Orders = new List<TradeOrder>()
        };

        decimal cumulativeGains = 0m;
        decimal totalVolume = 0m;

        // Custom Trade Directives: If advisor specified exact trades (e.g. "sell 2 shares on apple and 10 shares on msft")
        if (customTradeInstructions != null && customTradeInstructions.Count > 0)
        {
            decimal totalSellProceeds = 0m;

            foreach (var instruction in customTradeInstructions)
            {
                var pos = account.Positions.FirstOrDefault(p => p.Symbol.Equals(instruction.Symbol, StringComparison.OrdinalIgnoreCase));
                if (pos == null) continue;

                decimal targetShares = instruction.Shares;
                if (instruction.AmountType == TradeAmountType.Percent && instruction.RawAmount.HasValue && instruction.RawAmount.Value > 0)
                {
                    targetShares = Math.Round(pos.Shares * (instruction.RawAmount.Value / 100m), 2);
                }
                else if (instruction.AmountType == TradeAmountType.Dollars && instruction.RawAmount.HasValue && instruction.RawAmount.Value > 0 && pos.CurrentMarketPrice > 0)
                {
                    targetShares = Math.Round(instruction.RawAmount.Value / pos.CurrentMarketPrice, 2);
                }

                var sharesToTrade = instruction.Action == TradeAction.Sell 
                    ? Math.Min(pos.Shares, targetShares) 
                    : targetShares;

                if (sharesToTrade <= 0) continue;

                var tradeVal = Math.Round(sharesToTrade * pos.CurrentMarketPrice, 2);
                decimal gain = 0m;

                if (instruction.Action == TradeAction.Sell)
                {
                    var gainPerShare = Math.Max(0, pos.CurrentMarketPrice - pos.CostBasisPerShare);
                    gain = Math.Round(sharesToTrade * gainPerShare, 2);
                    cumulativeGains += gain;
                    totalSellProceeds += tradeVal;
                }

                totalVolume += tradeVal;

                proposal.Orders.Add(new TradeOrder
                {
                    Action = instruction.Action,
                    Symbol = pos.Symbol,
                    SecurityName = pos.SecurityName,
                    AssetClass = pos.AssetClass,
                    Shares = sharesToTrade,
                    EstimatedPrice = pos.CurrentMarketPrice,
                    EstimatedCapitalGain = gain
                });
            }

            // If sells occurred, reinvest proceeds into fixed income to maintain rebalance discipline
            if (totalSellProceeds > 0)
            {
                var bondPos = account.Positions.FirstOrDefault(p => p.AssetClass == AssetClass.FixedIncome);
                var bondSymbol = bondPos?.Symbol ?? "BND";
                var bondName = bondPos?.SecurityName ?? "Vanguard Total Bond Market ETF";
                var bondPrice = bondPos?.CurrentMarketPrice ?? 80m;

                var sharesToBuy = Math.Round(totalSellProceeds / bondPrice, 2);
                if (sharesToBuy > 0)
                {
                    totalVolume += totalSellProceeds;
                    proposal.Orders.Add(new TradeOrder
                    {
                        Action = TradeAction.Buy,
                        Symbol = bondSymbol,
                        SecurityName = bondName,
                        AssetClass = AssetClass.FixedIncome,
                        Shares = sharesToBuy,
                        EstimatedPrice = bondPrice,
                        EstimatedCapitalGain = 0m
                    });
                }
            }

            proposal.TotalEstimatedCapitalGains = Math.Round(cumulativeGains, 2);
            proposal.TotalTradeVolume = Math.Round(totalVolume, 2);
            return proposal;
        }

        // If equities are overweight, we sell equities down to target
        if (equitiesExcessValue > 0)
        {
            var eligibleEquityPositions = account.Positions
                .Where(p => p.AssetClass == AssetClass.Equities)
                .Where(p => excludedSymbols == null || !excludedSymbols.Contains(p.Symbol, StringComparer.OrdinalIgnoreCase))
                .OrderBy(p => p.UnrealizedGain / (p.TotalValue > 0 ? p.TotalValue : 1)) // Tax-smart: sell lowest gain/loss first
                .ToList();

            decimal remainingToSell = equitiesExcessValue;
            var eligibleEquitiesValue = eligibleEquityPositions.Sum(p => p.TotalValue);

            foreach (var pos in eligibleEquityPositions)
            {
                if (remainingToSell <= 0) break;

                // Proportionally trim excess across eligible equity positions
                var targetTrimValue = (pos == eligibleEquityPositions.Last() || eligibleEquitiesValue <= 0) 
                    ? remainingToSell 
                    : Math.Round(equitiesExcessValue * (pos.TotalValue / eligibleEquitiesValue), 2);
                var sellValue = Math.Min(remainingToSell, Math.Min(pos.TotalValue, targetTrimValue));

                var sharesToSell = Math.Round(sellValue / pos.CurrentMarketPrice, 2);
                if (sharesToSell <= 0) continue;

                var realizedGainPerShare = Math.Max(0, pos.CurrentMarketPrice - pos.CostBasisPerShare);
                var totalGain = sharesToSell * realizedGainPerShare;

                // Tax Budget Guardrail: scale down sharesToSell to stay within budget
                if (maxCapitalGainsBudget.HasValue)
                {
                    var remainingBudget = maxCapitalGainsBudget.Value - cumulativeGains;
                    if (remainingBudget <= 0)
                    {
                        break; // Tax budget fully utilized
                    }

                    if (totalGain > remainingBudget && realizedGainPerShare > 0)
                    {
                        // Scale down shares to fit remaining tax budget
                        sharesToSell = Math.Floor((remainingBudget / realizedGainPerShare) * 100m) / 100m;
                        if (sharesToSell <= 0) continue;
                        totalGain = sharesToSell * realizedGainPerShare;
                    }
                }

                cumulativeGains += totalGain;
                var tradeVal = sharesToSell * pos.CurrentMarketPrice;
                totalVolume += tradeVal;
                remainingToSell -= tradeVal;

                proposal.Orders.Add(new TradeOrder
                {
                    Action = TradeAction.Sell,
                    Symbol = pos.Symbol,
                    SecurityName = pos.SecurityName,
                    AssetClass = AssetClass.Equities,
                    Shares = sharesToSell,
                    EstimatedPrice = pos.CurrentMarketPrice,
                    EstimatedCapitalGain = Math.Round(totalGain, 2)
                });
            }

            // Direct proceeds into underweight asset class: Fixed Income (e.g. BND)
            var bondPosition = account.Positions.FirstOrDefault(p => p.AssetClass == AssetClass.FixedIncome);
            var bondSymbol = bondPosition?.Symbol ?? "BND";
            var bondName = bondPosition?.SecurityName ?? "Vanguard Total Bond Market ETF";
            var bondPrice = bondPosition?.CurrentMarketPrice ?? 80m;

            var buyValue = totalVolume; // Reinvest the sold volume
            var sharesToBuy = Math.Round(buyValue / bondPrice, 2);

            if (sharesToBuy > 0)
            {
                totalVolume += buyValue;
                proposal.Orders.Add(new TradeOrder
                {
                    Action = TradeAction.Buy,
                    Symbol = bondSymbol,
                    SecurityName = bondName,
                    AssetClass = AssetClass.FixedIncome,
                    Shares = sharesToBuy,
                    EstimatedPrice = bondPrice,
                    EstimatedCapitalGain = 0m
                });
            }
        }

        proposal.TotalEstimatedCapitalGains = Math.Round(cumulativeGains, 2);
        proposal.TotalTradeVolume = Math.Round(totalVolume, 2);

        return proposal;
    }
}
