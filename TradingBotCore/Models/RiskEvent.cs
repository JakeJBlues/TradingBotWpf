using System;
using System.Collections.Generic;

namespace TradingBotWPF.Models;

public partial class RiskEvent
{
    public Guid EventId { get; set; }

    public Guid SessionId { get; set; }

    public string EventType { get; set; } = null!;

    public DateTime EventTime { get; set; }

    public decimal? TriggerValue { get; set; }

    public decimal? ThresholdValue { get; set; }

    public decimal PortfolioValueAtEvent { get; set; }

    public int ActivePositionsCount { get; set; }

    public string ActionTaken { get; set; } = null!;

    public int? AffectedPositions { get; set; }

    public string? Description { get; set; }

    public virtual TradingSession Session { get; set; } = null!;
}
