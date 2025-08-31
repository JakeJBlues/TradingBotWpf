using System;
using System.Collections.Generic;

namespace TradingBotWPF.Models;

public partial class DailyPerformance
{
    public Guid DailyId { get; set; }

    public Guid SessionId { get; set; }

    public DateOnly TradeDate { get; set; }

    public int TotalTrades { get; set; }

    public int SuccessfulTrades { get; set; }

    public int FailedTrades { get; set; }

    public int AverageDownTrades { get; set; }

    public decimal TotalPnL { get; set; }

    public decimal TotalFees { get; set; }

    public decimal NetPnL { get; set; }

    public decimal WinRate { get; set; }

    public decimal MaxDrawdown { get; set; }

    public decimal MaxPositionSize { get; set; }

    public decimal PortfolioValue { get; set; }

    public decimal TotalVolumeTraded { get; set; }

    public int FilterBlockedTrades { get; set; }

    public int FilterCorrectBlocks { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual TradingSession Session { get; set; } = null!;
}
