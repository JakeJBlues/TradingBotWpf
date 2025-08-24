using System;
using System.Collections.Generic;

namespace TradingBotWPF.Models;

public partial class VwFilterEffectiveness
{
    public Guid SessionId { get; set; }

    public string FilterType { get; set; } = null!;

    public int? TotalDecisions { get; set; }

    public int? BlockedTrades { get; set; }

    public int? AllowedTrades { get; set; }

    public int? CorrectBlocks { get; set; }

    public int? IncorrectBlocks { get; set; }

    public decimal? AvgMissedOpportunity { get; set; }

    public double? FilterAccuracy { get; set; }
}
