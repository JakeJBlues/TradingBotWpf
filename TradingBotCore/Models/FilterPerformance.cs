using System;
using System.Collections.Generic;

namespace TradingBotWPF.Models;

public partial class FilterPerformance
{
    public Guid FilterId { get; set; }

    public Guid SessionId { get; set; }

    public string Symbol { get; set; } = null!;

    public string FilterType { get; set; } = null!;

    public decimal? FilterValue { get; set; }

    public decimal? FilterThreshold { get; set; }

    public string Decision { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    public bool OutcomeKnown { get; set; }

    public bool? WouldHaveBeenProfitable { get; set; }

    public decimal? MissedProfitLoss { get; set; }

    public virtual TradingSession Session { get; set; } = null!;
}
