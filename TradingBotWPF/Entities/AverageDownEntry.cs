using System;

namespace TradingBotWPF.Entities
{
    // Hilfsklasse für Average-Down Historie
    public class AverageDownEntry
    {
        public DateTime Timestamp { get; set; }
        public decimal Price { get; set; }
        public double Volume { get; set; }
        public decimal InvestedAmount { get; set; }
        public decimal PreviousAveragePrice { get; set; }
    }
}