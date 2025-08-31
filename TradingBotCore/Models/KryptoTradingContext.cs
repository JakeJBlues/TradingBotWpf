using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace TradingBotWPF.Models;

public partial class KryptoTradingContext : DbContext
{
    public KryptoTradingContext()
    {
    }

    public KryptoTradingContext(DbContextOptions<KryptoTradingContext> options)
        : base(options)
    {
    }

    public virtual DbSet<DailyPerformance> DailyPerformances { get; set; }

    public virtual DbSet<FilterPerformance> FilterPerformances { get; set; }

    public virtual DbSet<MarketCondition> MarketConditions { get; set; }

    public virtual DbSet<Position> Positions { get; set; }

    public virtual DbSet<RiskEvent> RiskEvents { get; set; }

    public virtual DbSet<SymbolPerformance> SymbolPerformances { get; set; }

    public virtual DbSet<TradingSession> TradingSessions { get; set; }

    public virtual DbSet<VwDailyTrend> VwDailyTrends { get; set; }

    public virtual DbSet<VwFilterEffectiveness> VwFilterEffectivenesses { get; set; }

    public virtual DbSet<VwOverallPerformance> VwOverallPerformances { get; set; }

    public virtual DbSet<VwTopPerformingSymbol> VwTopPerformingSymbols { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Server=peanutshostingug.database.windows.net;Database=KryptoTrading;User Id=peanutshostingug;Password=pGlennyweg2311!g;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyPerformance>(entity =>
        {
            entity.HasKey(e => e.DailyId).HasName("PK__DailyPer__650EC01796558855");

            entity.ToTable("DailyPerformance");

            entity.HasIndex(e => e.TradeDate, "IX_DailyPerformance_Date");

            entity.HasIndex(e => new { e.SessionId, e.TradeDate }, "UQ_DailyPerformance_SessionDate").IsUnique();

            entity.Property(e => e.DailyId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.MaxDrawdown).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.MaxPositionSize).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.NetPnL).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.PortfolioValue).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.TotalFees).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.TotalPnL).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.TotalVolumeTraded).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.WinRate).HasColumnType("decimal(10, 4)");

            entity.HasOne(d => d.Session).WithMany(p => p.DailyPerformances)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("FK_DailyPerformance_Sessions");
        });

        modelBuilder.Entity<FilterPerformance>(entity =>
        {
            entity.HasKey(e => e.FilterId).HasName("PK__FilterPe__3159DF6E4D90906F");

            entity.ToTable("FilterPerformance");

            entity.HasIndex(e => new { e.SessionId, e.FilterType }, "IX_FilterPerformance_SessionType");

            entity.HasIndex(e => e.Timestamp, "IX_FilterPerformance_Timestamp");

            entity.Property(e => e.FilterId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Decision).HasMaxLength(20);
            entity.Property(e => e.FilterThreshold).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.FilterType).HasMaxLength(50);
            entity.Property(e => e.FilterValue).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.MissedProfitLoss).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.Symbol).HasMaxLength(20);
            entity.Property(e => e.Timestamp).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.Session).WithMany(p => p.FilterPerformances)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("FK_FilterPerformance_Sessions");
        });

        modelBuilder.Entity<MarketCondition>(entity =>
        {
            entity.HasKey(e => e.ConditionId).HasName("PK__MarketCo__37F5C0CFCFD99661");

            entity.HasIndex(e => e.Timestamp, "IX_MarketConditions_Timestamp");

            entity.Property(e => e.ConditionId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.AverageVolatility).HasColumnType("decimal(10, 4)");
            entity.Property(e => e.MarketTrend).HasMaxLength(20);
            entity.Property(e => e.NewsEvents).HasMaxLength(500);
            entity.Property(e => e.Timestamp).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.TopSymbolVolatility).HasColumnType("decimal(10, 4)");
            entity.Property(e => e.TotalMarketVolume).HasColumnType("decimal(18, 8)");

            entity.HasOne(d => d.Session).WithMany(p => p.MarketConditions)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("FK_MarketConditions_Sessions");
        });

        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasKey(e => e.PositionId).HasName("PK__Position__60BB9A79B50508FE");

            entity.HasIndex(e => e.EntryTime, "IX_Positions_EntryTime");

            entity.HasIndex(e => new { e.SessionId, e.Symbol }, "IX_Positions_SessionSymbol");

            entity.HasIndex(e => e.PositionStatus, "IX_Positions_Status");

            entity.Property(e => e.PositionId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.AverageEntryPrice).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.EntryPrice).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.EntryTime).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.ExitPrice).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.ExitReason).HasMaxLength(50);
            entity.Property(e => e.OriginalEntryPrice).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.PositionStatus)
                .HasMaxLength(20)
                .HasDefaultValue("Open");
            entity.Property(e => e.PriceDistanceFrom30DayHigh).HasColumnType("decimal(10, 4)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.RealizedPnL).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.RsiatEntry)
                .HasColumnType("decimal(10, 4)")
                .HasColumnName("RSIAtEntry");
            entity.Property(e => e.Symbol).HasMaxLength(20);
            entity.Property(e => e.TotalInvestment).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.UnrealizedPnL).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.VolatilityAtEntry).HasColumnType("decimal(10, 4)");
            entity.Property(e => e.VolumeAtEntry).HasColumnType("decimal(18, 8)");

            entity.HasOne(d => d.Session).WithMany(p => p.Positions)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("FK_Positions_Sessions");
        });

        modelBuilder.Entity<RiskEvent>(entity =>
        {
            entity.HasKey(e => e.EventId).HasName("PK__RiskEven__7944C8101F4597D3");

            entity.Property(e => e.EventId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ActionTaken).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.EventTime).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.EventType).HasMaxLength(50);
            entity.Property(e => e.PortfolioValueAtEvent).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.ThresholdValue).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.TriggerValue).HasColumnType("decimal(18, 8)");

            entity.HasOne(d => d.Session).WithMany(p => p.RiskEvents)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("FK_RiskEvents_Sessions");
        });

        modelBuilder.Entity<SymbolPerformance>(entity =>
        {
            entity.HasKey(e => e.SymbolPerfId).HasName("PK__SymbolPe__361681795FB9FEFC");

            entity.ToTable("SymbolPerformance");

            entity.HasIndex(e => new { e.SessionId, e.Symbol }, "UQ_SymbolPerformance_SessionSymbol").IsUnique();

            entity.Property(e => e.SymbolPerfId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.AverageHoldTime).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.AveragePnLperTrade)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("AveragePnLPerTrade");
            entity.Property(e => e.BestTrade).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.LastUpdated).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.LongestHoldTime).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.MaxLossOnSymbol).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.QuickestProfitTime).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Symbol).HasMaxLength(20);
            entity.Property(e => e.TotalPnL).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.VolatilityScore).HasColumnType("decimal(10, 4)");
            entity.Property(e => e.WinRate).HasColumnType("decimal(10, 4)");
            entity.Property(e => e.WorstTrade).HasColumnType("decimal(18, 8)");

            entity.HasOne(d => d.Session).WithMany(p => p.SymbolPerformances)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("FK_SymbolPerformance_Sessions");
        });

        modelBuilder.Entity<TradingSession>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("PK__TradingS__C9F4929071F30B77");

            entity.Property(e => e.SessionId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.BotVersion)
                .HasMaxLength(50)
                .HasDefaultValue("Enhanced-v2.0");
            entity.Property(e => e.ConfigurationHash).HasMaxLength(256);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.FinalBalance).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.InitialBalance).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.StartTime).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Running");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getutcdate())");
        });

        modelBuilder.Entity<VwDailyTrend>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_DailyTrend");

            entity.Property(e => e.CumulativePnL).HasColumnType("decimal(38, 8)");
            entity.Property(e => e.NetPnL).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.WinRate).HasColumnType("decimal(10, 4)");
        });

        modelBuilder.Entity<VwFilterEffectiveness>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_FilterEffectiveness");

            entity.Property(e => e.AvgMissedOpportunity).HasColumnType("decimal(38, 8)");
            entity.Property(e => e.FilterType).HasMaxLength(50);
        });

        modelBuilder.Entity<VwOverallPerformance>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_OverallPerformance");

            entity.Property(e => e.AvgPnLperTrade)
                .HasColumnType("decimal(38, 8)")
                .HasColumnName("AvgPnLPerTrade");
            entity.Property(e => e.BestTrade).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.FinalBalance).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.InitialBalance).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.RoiPercent)
                .HasColumnType("decimal(38, 15)")
                .HasColumnName("ROI_Percent");
            entity.Property(e => e.TotalPnL).HasColumnType("decimal(19, 8)");
            entity.Property(e => e.WorstTrade).HasColumnType("decimal(18, 8)");
        });

        modelBuilder.Entity<VwTopPerformingSymbol>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_TopPerformingSymbols");

            entity.Property(e => e.AveragePnLperTrade)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("AveragePnLPerTrade");
            entity.Property(e => e.Symbol).HasMaxLength(20);
            entity.Property(e => e.TotalPnL).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.WinRate).HasColumnType("decimal(10, 4)");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
