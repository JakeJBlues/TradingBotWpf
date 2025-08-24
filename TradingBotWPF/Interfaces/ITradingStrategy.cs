using System.Collections.Generic;

namespace TradingBotWPF.Interfaces
{
    public interface ITradingStrategy
    {
        bool ShouldBuy(List<double> prices, decimal currentPrice);
    }
}