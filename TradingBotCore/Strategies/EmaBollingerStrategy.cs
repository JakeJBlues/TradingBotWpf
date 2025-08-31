using System.Collections.Generic;
using System.Linq;
using Serilog;
using TradingBotCore.Interfaces;

namespace TradingBotCore.Strategies
{
    public class EmaBollingerStrategy : ITradingStrategy
    {
        public bool ShouldBuy(List<double> prices, decimal currentPrice)
        {
            // Vereinfachte Strategie - kaufen wenn Preis im unteren Bereich
            if (prices.Count < 10) return false;
            var pricesLast = prices.Take(3).ToList();
            var min = prices.Min();
            var max = prices.Max();
            var range = max - min;
            Log.Debug($"EmaBollingerStrategy: {(double)currentPrice <= min + range * 0.1} Min={min}, Max={max}, Range={range}, CurrentPrice={currentPrice}");
            return (double)currentPrice <= min + range / 2;// && pricesLast[0] >= pricesLast[1] && pricesLast[1] >= pricesLast[2];
        }
    }
}