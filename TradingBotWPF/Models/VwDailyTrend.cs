using System;
using System.Collections.Generic;

namespace TradingBotWPF.Models;

public partial class VwDailyTrend
{
    public Guid SessionId { get; set; }

    public DateOnly TradeDate { get; set; }

    public decimal NetPnL { get; set; }

    public decimal? CumulativePnL { get; set; }

    public decimal WinRate { get; set; }

    public int TotalTrades { get; set; }

    public int FilterBlockedTrades { get; set; }

    public double? FilterAccuracy { get; set; }
}
