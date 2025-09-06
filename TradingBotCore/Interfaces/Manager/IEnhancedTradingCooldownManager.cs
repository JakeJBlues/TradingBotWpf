using TradingBotCore.Manager;

namespace TradingBotCore.Interfaces.Manager
{
    public interface IEnhancedTradingCooldownManager
    {
        List<CooldownInfo> GetAllActiveCooldowns();
        Dictionary<string, TimeSpan> GetBuyCooldowns();
        CooldownStatistics GetCooldownStatistics();
        TimeSpan GetGlobalCooldownRemaining();
        Dictionary<string, TimeSpan> GetSellCooldowns();
        void RecordBuy(string symbol);
        void RecordSell(string symbol);
    }
}