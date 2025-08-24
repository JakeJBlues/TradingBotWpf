using System;
using System.Collections.Generic;

namespace TradingBotWPF.Models;

public partial class TradingSession
{
    public Guid SessionId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public decimal InitialBalance { get; set; }

    public decimal? FinalBalance { get; set; }

    public string BotVersion { get; set; } = null!;

    public string ConfigurationHash { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<DailyPerformance> DailyPerformances { get; set; } = new List<DailyPerformance>();

    public virtual ICollection<FilterPerformance> FilterPerformances { get; set; } = new List<FilterPerformance>();

    public virtual ICollection<MarketCondition> MarketConditions { get; set; } = new List<MarketCondition>();

    public virtual ICollection<Position> Positions { get; set; } = new List<Position>();

    public virtual ICollection<RiskEvent> RiskEvents { get; set; } = new List<RiskEvent>();

    public virtual ICollection<SymbolPerformance> SymbolPerformances { get; set; } = new List<SymbolPerformance>();
}
