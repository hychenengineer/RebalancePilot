using TamaracCoPilot.Api.Application.Services;
using TamaracCoPilot.Api.Domain.Entities;
using TamaracCoPilot.Api.Domain.Enums;
using Xunit;

namespace TamaracCoPilot.Tests;

public class RebalanceServiceTests
{
    private readonly RebalanceService _service;

    public RebalanceServiceTests()
    {
        _service = new RebalanceService();
    }

    private Account CreateSarahJenkinsAccount()
    {
        var model = new ModelPortfolio
        {
            Id = 1,
            ModelName = "60/40 Balanced Growth",
            TargetEquitiesPct = 0.60m,
            TargetFixedIncomePct = 0.40m,
            ToleranceBandPct = 0.05m
        };

        var account = new Account
        {
            Id = 1,
            AccountNumber = "ENV-100942",
            ClientName = "Sarah Jenkins",
            TargetModel = model,
            TargetModelId = model.Id,
            CashBalance = 0m,
            ConcurrencyToken = Guid.NewGuid(),
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "AAPL",
                    SecurityName = "Apple Inc.",
                    AssetClass = AssetClass.Equities,
                    Shares = 250m,
                    CostBasisPerShare = 150m,
                    CurrentMarketPrice = 200m // $50,000
                },
                new Position
                {
                    Symbol = "MSFT",
                    SecurityName = "Microsoft Corp.",
                    AssetClass = AssetClass.Equities,
                    Shares = 62.5m,
                    CostBasisPerShare = 320m,
                    CurrentMarketPrice = 400m // $25,000
                },
                new Position
                {
                    Symbol = "BND",
                    SecurityName = "Vanguard Total Bond Market ETF",
                    AssetClass = AssetClass.FixedIncome,
                    Shares = 312.5m,
                    CostBasisPerShare = 80m,
                    CurrentMarketPrice = 80m // $25,000
                }
            }
        };

        return account;
    }

    [Fact]
    public void CalculateDrift_CalculatesExactPercentagesAndFlagsExceededTolerance()
    {
        // Arrange
        var account = CreateSarahJenkinsAccount();

        // Act
        var result = _service.CalculateDrift(account);

        // Assert
        Assert.Equal(100000m, result.TotalAum);
        Assert.Equal(0.7500m, result.CurrentEquitiesPct);
        Assert.Equal(0.2500m, result.CurrentFixedIncomePct);
        Assert.Equal(0.1500m, result.EquitiesDriftPct); // +15% drift
        Assert.Equal(-0.1500m, result.FixedIncomeDriftPct);
        Assert.True(result.IsDriftToleranceExceeded);
    }

    [Fact]
    public void GenerateRebalanceProposal_EmitsBuyAndSellOrdersToRestoreBalance()
    {
        // Arrange
        var account = CreateSarahJenkinsAccount();

        // Act
        var proposal = _service.GenerateRebalanceProposal(account);

        // Assert
        Assert.NotNull(proposal);
        Assert.NotEmpty(proposal.Orders);

        var sellOrders = proposal.Orders.Where(o => o.Action == TradeAction.Sell).ToList();
        var buyOrders = proposal.Orders.Where(o => o.Action == TradeAction.Buy).ToList();

        Assert.NotEmpty(sellOrders);
        Assert.NotEmpty(buyOrders);

        // The buy order should be BND (Fixed Income)
        Assert.Contains(buyOrders, o => o.Symbol == "BND");

        // Fiduciary balance: sell volume should equal buy volume
        var totalSellVolume = sellOrders.Sum(s => s.EstimatedValue);
        var totalBuyVolume = buyOrders.Sum(b => b.EstimatedValue);
        Assert.Equal(totalSellVolume, totalBuyVolume);
    }

    [Fact]
    public void GenerateRebalanceProposal_RespectsCapitalGainsBudget()
    {
        // Arrange
        var account = CreateSarahJenkinsAccount();
        decimal maxTaxBudget = 1500m;

        // Act
        var proposal = _service.GenerateRebalanceProposal(account, maxCapitalGainsBudget: maxTaxBudget);

        // Assert
        Assert.True(proposal.TotalEstimatedCapitalGains <= maxTaxBudget,
            $"Expected gains <= {maxTaxBudget}, but got {proposal.TotalEstimatedCapitalGains}");
    }

    [Fact]
    public void GenerateRebalanceProposal_ExecutesPercentageCustomTrades()
    {
        // Arrange
        var account = CreateSarahJenkinsAccount();
        // AAPL has 250 shares. 10% trim = 25 shares.
        var instructions = new List<SpecificTradeInstruction>
        {
            new SpecificTradeInstruction(TradeAction.Sell, "AAPL", 0m, TamaracCoPilot.Api.Domain.Contracts.TradeAmountType.Percent, 10m)
        };

        // Act
        var proposal = _service.GenerateRebalanceProposal(account, customTradeInstructions: instructions);

        // Assert
        Assert.NotNull(proposal);
        var aaplOrder = proposal.Orders.FirstOrDefault(o => o.Symbol == "AAPL" && o.Action == TradeAction.Sell);
        Assert.NotNull(aaplOrder);
        Assert.Equal(25m, aaplOrder.Shares); // 10% of 250 shares = 25 shares
    }
}
