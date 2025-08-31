using System.Collections.Generic;

namespace TradingBotCore.Interfaces
{
    public interface ITradingStrategy
    {
        bool ShouldBuy(List<double> prices, decimal currentPrice);
    }
}