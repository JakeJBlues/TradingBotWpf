namespace TradingBotCore.Interfaces.Manager
{
    public interface ITradingBlacklistManager
    {
        void AddAssetToBlacklist(string asset);
        void AddSymbolToBlacklist(string symbol);
        List<string> FilterAllowedSymbols(IEnumerable<string> symbols);
        (int BlacklistedAssets, int BlacklistedSymbols, List<string> Assets, List<string> Symbols) GetBlacklistStatus();
        bool IsSymbolAllowed(string symbol);
        void LogBlacklistStatus();
        void RemoveAssetFromBlacklist(string asset);
        void RemoveSymbolFromBlacklist(string symbol);
    }
}