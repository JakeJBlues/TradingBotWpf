namespace TradingBotCore.Interfaces.Manager
{
    public interface ITradingCooldownManager
    {
        bool CanBuy(string symbol);
        bool CanSell(string symbol);
        void CleanupOldEntries(TimeSpan maxAge);
        Dictionary<string, TimeSpan> GetActiveLockouts();
        void RecordBuy(string symbol);
        void RecordSell(string symbol);
    }
}