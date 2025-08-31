using System;
using System.Collections.Generic;

namespace TradingBotWPF.Models;

public partial class VwOverallPerformance
{
    public Guid SessionId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public decimal InitialBalance { get; set; }

    public decimal? FinalBalance { get; set; }

    public decimal? TotalPnL { get; set; }

    public decimal? RoiPercent { get; set; }

    public int? TotalTrades { get; set; }

    public int? WinningTrades { get; set; }

    public double? WinRate { get; set; }

    public decimal? AvgPnLperTrade { get; set; }

    public decimal? BestTrade { get; set; }

    public decimal? WorstTrade { get; set; }

    public int? TotalAverageDowns { get; set; }
}
