using Microsoft.EntityFrameworkCore;
using TamaracCoPilot.Api.Domain.Entities;

namespace TamaracCoPilot.Api.Infrastructure.Data;

public class TamaracDbContext : DbContext
{
    public TamaracDbContext(DbContextOptions<TamaracDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<ModelPortfolio> ModelPortfolios => Set<ModelPortfolio>();
    public DbSet<TradeProposal> TradeProposals => Set<TradeProposal>();
    public DbSet<TradeOrder> TradeOrders => Set<TradeOrder>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Account configuration
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.AccountNumber).IsRequired().HasMaxLength(50);
            entity.Property(a => a.ClientName).IsRequired().HasMaxLength(150);
            entity.Property(a => a.CashBalance).HasPrecision(18, 4);
            entity.Property(a => a.ConcurrencyToken).IsConcurrencyToken();

            entity.HasOne(a => a.TargetModel)
                  .WithMany()
                  .HasForeignKey(a => a.TargetModelId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(a => a.Positions)
                  .WithOne()
                  .HasForeignKey(p => p.AccountId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Position configuration
        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Symbol).IsRequired().HasMaxLength(20);
            entity.Property(p => p.SecurityName).IsRequired().HasMaxLength(150);
            entity.Property(p => p.Shares).HasPrecision(18, 4);
            entity.Property(p => p.CostBasisPerShare).HasPrecision(18, 4);
            entity.Property(p => p.CurrentMarketPrice).HasPrecision(18, 4);
        });

        // ModelPortfolio configuration
        modelBuilder.Entity<ModelPortfolio>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.ModelName).IsRequired().HasMaxLength(100);
            entity.Property(m => m.TargetEquitiesPct).HasPrecision(8, 4);
            entity.Property(m => m.TargetFixedIncomePct).HasPrecision(8, 4);
            entity.Property(m => m.TargetCashPct).HasPrecision(8, 4);
            entity.Property(m => m.ToleranceBandPct).HasPrecision(8, 4);
        });

        // TradeProposal configuration
        modelBuilder.Entity<TradeProposal>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TotalEstimatedCapitalGains).HasPrecision(18, 4);
            entity.Property(t => t.TotalTradeVolume).HasPrecision(18, 4);
            entity.HasMany(t => t.Orders)
                  .WithOne()
                  .HasForeignKey(o => o.TradeProposalId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // TradeOrder configuration
        modelBuilder.Entity<TradeOrder>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Symbol).IsRequired().HasMaxLength(20);
            entity.Property(o => o.SecurityName).IsRequired().HasMaxLength(150);
            entity.Property(o => o.Shares).HasPrecision(18, 4);
            entity.Property(o => o.EstimatedPrice).HasPrecision(18, 4);
            entity.Property(o => o.EstimatedCapitalGain).HasPrecision(18, 4);
        });
    }
}
