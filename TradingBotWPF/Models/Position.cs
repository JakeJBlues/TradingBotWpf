using System;
using System.Collections.Generic;

namespace TradingBotWPF.Models;

public partial class Position
{
    public Guid PositionId { get; set; }

    public Guid SessionId { get; set; }

    public string Symbol { get; set; } = null!;

    public DateTime EntryTime { get; set; }

    public DateTime? ExitTime { get; set; }

    public decimal EntryPrice { get; set; }

    public decimal? ExitPrice { get; set; }

    public decimal Quantity { get; set; }

    public decimal TotalInvestment { get; set; }

    public decimal? RealizedPnL { get; set; }

    public decimal? UnrealizedPnL { get; set; }

    public int AverageDownCount { get; set; }

    public decimal OriginalEntryPrice { get; set; }

    public decimal AverageEntryPrice { get; set; }

    public string? ExitReason { get; set; }

    public decimal? VolatilityAtEntry { get; set; }

    public decimal? VolumeAtEntry { get; set; }

    public decimal? RsiatEntry { get; set; }

    public decimal? PriceDistanceFrom30DayHigh { get; set; }

    public string PositionStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual TradingSession Session { get; set; } = null!;
}
