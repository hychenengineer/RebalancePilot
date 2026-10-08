using Microsoft.EntityFrameworkCore;
using TamaracCoPilot.Api.Domain.Entities;
using TamaracCoPilot.Api.Domain.Enums;

namespace TamaracCoPilot.Api.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(TamaracDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        if (await context.Accounts.AnyAsync())
        {
            return; // DB has already been seeded
        }

        // 1. Target Models
        var balancedModel = new ModelPortfolio
        {
            ModelName = "60/40 Balanced Growth",
            Description = "Core institutional model targeting 60% equities and 40% fixed income.",
            TargetEquitiesPct = 0.60m,
            TargetFixedIncomePct = 0.40m,
            TargetCashPct = 0.00m,
            ToleranceBandPct = 0.05m // 5% drift threshold
        };

        var aggressiveGrowthModel = new ModelPortfolio
        {
            ModelName = "80/20 Aggressive Growth",
            Description = "High-growth capital appreciation targeting 80% equities and 20% fixed income.",
            TargetEquitiesPct = 0.80m,
            TargetFixedIncomePct = 0.20m,
            TargetCashPct = 0.00m,
            ToleranceBandPct = 0.05m
        };

        context.ModelPortfolios.AddRange(balancedModel, aggressiveGrowthModel);
        await context.SaveChangesAsync();

        // 2. Seed Sarah Jenkins ($100,000 total AUM, drifted to 75% stocks / 25% bonds)
        var sarahAccount = new Account
        {
            AccountNumber = "ENV-100942",
            ClientName = "Sarah Jenkins",
            Custodian = "Charles Schwab Institutional",
            CashBalance = 0.00m,
            TargetModelId = balancedModel.Id,
            ConcurrencyToken = Guid.NewGuid(),
            UpdatedAt = DateTime.UtcNow
        };

        sarahAccount.Positions = new List<Position>
        {
            // 1. Tech & Artificial Intelligence ($25,000 total)
            new Position
            {
                Symbol = "NVDA",
                SecurityName = "NVIDIA Corporation (AI / Semiconductors)",
                AssetClass = AssetClass.Equities,
                Shares = 100m,
                CostBasisPerShare = 90m,
                CurrentMarketPrice = 140m // $14,000
            },
            new Position
            {
                Symbol = "MSFT",
                SecurityName = "Microsoft Corporation (AI / Cloud Software)",
                AssetClass = AssetClass.Equities,
                Shares = 27.5m,
                CostBasisPerShare = 320m,
                CurrentMarketPrice = 400m // $11,000
            },

            // 2. Healthcare & Pharmaceuticals ($15,000 total)
            new Position
            {
                Symbol = "LLY",
                SecurityName = "Eli Lilly and Co. (Healthcare / Pharma)",
                AssetClass = AssetClass.Equities,
                Shares = 11.25m,
                CostBasisPerShare = 650m,
                CurrentMarketPrice = 800m // $9,000
            },
            new Position
            {
                Symbol = "JNJ",
                SecurityName = "Johnson & Johnson (Healthcare / MedTech)",
                AssetClass = AssetClass.Equities,
                Shares = 40m,
                CostBasisPerShare = 140m,
                CurrentMarketPrice = 150m // $6,000
            },

            // 3. Financial Services ($15,000 total)
            new Position
            {
                Symbol = "JPM",
                SecurityName = "JPMorgan Chase & Co. (Financials / Banking)",
                AssetClass = AssetClass.Equities,
                Shares = 45m,
                CostBasisPerShare = 160m,
                CurrentMarketPrice = 200m // $9,000
            },
            new Position
            {
                Symbol = "V",
                SecurityName = "Visa Inc. (Financials / Payments)",
                AssetClass = AssetClass.Equities,
                Shares = 22.22m,
                CostBasisPerShare = 230m,
                CurrentMarketPrice = 270m // $6,000
            },

            // 4. Energy & Renewables ($10,000 total)
            new Position
            {
                Symbol = "XOM",
                SecurityName = "Exxon Mobil Corp. (Energy / Oil & Gas)",
                AssetClass = AssetClass.Equities,
                Shares = 50m,
                CostBasisPerShare = 95m,
                CurrentMarketPrice = 120m // $6,000
            },
            new Position
            {
                Symbol = "NEE",
                SecurityName = "NextEra Energy Inc. (Clean Energy / Utilities)",
                AssetClass = AssetClass.Equities,
                Shares = 50m,
                CostBasisPerShare = 60m,
                CurrentMarketPrice = 80m // $4,000
            },

            // 5. Consumer Goods & Retail ($10,000 total)
            new Position
            {
                Symbol = "WMT",
                SecurityName = "Walmart Inc. (Consumer Retail)",
                AssetClass = AssetClass.Equities,
                Shares = 75m,
                CostBasisPerShare = 65m,
                CurrentMarketPrice = 80m // $6,000
            },
            new Position
            {
                Symbol = "PG",
                SecurityName = "Procter & Gamble Co. (Consumer Staples)",
                AssetClass = AssetClass.Equities,
                Shares = 25m,
                CostBasisPerShare = 145m,
                CurrentMarketPrice = 160m // $4,000
            },

            // Fixed Income Benchmark ($25,000 total)
            new Position
            {
                Symbol = "BND",
                SecurityName = "Vanguard Total Bond Market ETF",
                AssetClass = AssetClass.FixedIncome,
                Shares = 312.5m,
                CostBasisPerShare = 80m,
                CurrentMarketPrice = 80m // $25,000 value
            }
        };

        // 3. Seed Robert Miller ($250,000 total AUM, drifted)
        var robertAccount = new Account
        {
            AccountNumber = "ENV-204118",
            ClientName = "Robert Miller",
            Custodian = "Fidelity Wealth Services",
            CashBalance = 5000.00m,
            TargetModelId = balancedModel.Id,
            ConcurrencyToken = Guid.NewGuid(),
            UpdatedAt = DateTime.UtcNow
        };

        robertAccount.Positions = new List<Position>
        {
            new Position
            {
                Symbol = "NVDA",
                SecurityName = "NVIDIA Corporation",
                AssetClass = AssetClass.Equities,
                Shares = 1000m,
                CostBasisPerShare = 80m,
                CurrentMarketPrice = 130m // $130,000 value
            },
            new Position
            {
                Symbol = "AMZN",
                SecurityName = "Amazon.com Inc.",
                AssetClass = AssetClass.Equities,
                Shares = 388.88m,
                CostBasisPerShare = 140m,
                CurrentMarketPrice = 180m // ~$70,000 value
            },
            new Position
            {
                Symbol = "AGG",
                SecurityName = "iShares Core U.S. Aggregate Bond ETF",
                AssetClass = AssetClass.FixedIncome,
                Shares = 459.18m,
                CostBasisPerShare = 98m,
                CurrentMarketPrice = 98m // ~$45,000 value
            }
        };

        context.Accounts.AddRange(sarahAccount, robertAccount);
        await context.SaveChangesAsync();
    }
}
