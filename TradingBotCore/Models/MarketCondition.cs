using System;
using System.Collections.Generic;

namespace TradingBotWPF.Models;

public partial class MarketCondition
{
    public Guid ConditionId { get; set; }

    public Guid SessionId { get; set; }

    public DateTime Timestamp { get; set; }

    public decimal? TotalMarketVolume { get; set; }

    public int? VolatileSymbolsCount { get; set; }

    public decimal? AverageVolatility { get; set; }

    public decimal? TopSymbolVolatility { get; set; }

    public int ActivePositions { get; set; }

    public int SuccessfulTradesLast1H { get; set; }

    public int FailedTradesLast1H { get; set; }

    public string? MarketTrend { get; set; }

    public string? NewsEvents { get; set; }

    public virtual TradingSession Session { get; set; } = null!;
}
