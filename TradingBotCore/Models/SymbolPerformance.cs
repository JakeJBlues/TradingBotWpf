using System;
using System.Collections.Generic;

namespace TradingBotWPF.Models;

public partial class SymbolPerformance
{
    public Guid SymbolPerfId { get; set; }

    public Guid SessionId { get; set; }

    public string Symbol { get; set; } = null!;

    public int TotalTrades { get; set; }

    public int WinningTrades { get; set; }

    public int LosingTrades { get; set; }

    public int AverageDownCount { get; set; }

    public decimal TotalPnL { get; set; }

    public decimal AveragePnLperTrade { get; set; }

    public decimal BestTrade { get; set; }

    public decimal WorstTrade { get; set; }

    public decimal WinRate { get; set; }

    public decimal? AverageHoldTime { get; set; }

    public decimal? QuickestProfitTime { get; set; }

    public decimal? LongestHoldTime { get; set; }

    public decimal MaxLossOnSymbol { get; set; }

    public decimal? VolatilityScore { get; set; }

    public int FilterBlockCount { get; set; }

    public int FilterCorrectBlocks { get; set; }

    public DateTime LastUpdated { get; set; }

    public virtual TradingSession Session { get; set; } = null!;
}
