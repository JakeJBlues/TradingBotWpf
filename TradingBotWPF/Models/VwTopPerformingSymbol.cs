using System;
using System.Collections.Generic;

namespace TradingBotWPF.Models;

public partial class VwTopPerformingSymbol
{
    public Guid SessionId { get; set; }

    public string Symbol { get; set; } = null!;

    public decimal TotalPnL { get; set; }

    public decimal WinRate { get; set; }

    public int TotalTrades { get; set; }

    public decimal AveragePnLperTrade { get; set; }

    public long? ProfitRank { get; set; }

    public long? WinRateRank { get; set; }
}
