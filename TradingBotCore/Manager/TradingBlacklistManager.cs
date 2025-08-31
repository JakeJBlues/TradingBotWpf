using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace TradingBotCore.Manager
{
    // Interface für Trading-Strategien
    // NEU: Blacklist-Manager für unerwünschte Trading-Symbole
    public class TradingBlacklistManager
    {
        private readonly HashSet<string> _blacklistedSymbols = new();
        private readonly HashSet<string> _blacklistedAssets = new();
        private readonly object _lock = new object();

        public TradingBlacklistManager()
        {
            InitializeDefaultBlacklist();
        }

        private void InitializeDefaultBlacklist()
        {
            lock (_lock)
            {
                // Standardmäßig geblacklist Assets (können erweitert werden)
                var defaultBlacklistedAssets = new[]
                {
                    // Stablecoins (meist wenig volatil)
                    "USDT", "USDC", "BUSD", "DAI", "TUSD", "USDP", "FRAX", "EURC"
                };

                foreach (var asset in defaultBlacklistedAssets)
                {
                    _blacklistedAssets.Add(asset.ToUpper());
                }

                Log.Information($"Blacklist initialisiert: {_blacklistedAssets.Count} Assets, {_blacklistedSymbols.Count} Symbole");
                Log.Information($"Geblacklistete Assets: {string.Join(", ", _blacklistedAssets)}");

                if (_blacklistedSymbols.Any())
                {
                    Log.Information($"Geblacklistete Symbole: {string.Join(", ", _blacklistedSymbols)}");
                }
            }
        }

        // Asset zur Blacklist hinzufügen
        public void AddAssetToBlacklist(string asset)
        {
            lock (_lock)
            {
                if (_blacklistedAssets.Add(asset.ToUpper()))
                {
                    Log.Information($"Asset '{asset}' zur Blacklist hinzugefügt");
                }
            }
        }

        // Symbol zur Blacklist hinzufügen
        public void AddSymbolToBlacklist(string symbol)
        {
            lock (_lock)
            {
                if (_blacklistedSymbols.Add(symbol.ToUpper()))
                {
                    Log.Information($"Symbol '{symbol}' zur Blacklist hinzugefügt");
                }
            }
        }

        // Asset von Blacklist entfernen
        public void RemoveAssetFromBlacklist(string asset)
        {
            lock (_lock)
            {
                if (_blacklistedAssets.Remove(asset.ToUpper()))
                {
                    Log.Information($"Asset '{asset}' von Blacklist entfernt");
                }
            }
        }

        // Symbol von Blacklist entfernen
        public void RemoveSymbolFromBlacklist(string symbol)
        {
            lock (_lock)
            {
                if (_blacklistedSymbols.Remove(symbol.ToUpper()))
                {
                    Log.Information($"Symbol '{symbol}' von Blacklist entfernt");
                }
            }
        }

        // Hauptprüfung: Ist ein Symbol für Trading erlaubt?
        public bool IsSymbolAllowed(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return false;

            var upperSymbol = symbol.ToUpper();

            lock (_lock)
            {
                // 1. Prüfe vollständiges Symbol
                if (_blacklistedSymbols.Contains(upperSymbol))
                {
                    Log.Debug($"Symbol '{symbol}' ist explizit geblacklisted");
                    return false;
                }

                // 2. Extrahiere Asset aus Symbol (vor dem '-')
                var parts = upperSymbol.Split('-');
                if (parts.Length >= 1)
                {
                    var baseAsset = parts[0];

                    // 3. Prüfe Base-Asset gegen Blacklist
                    if (_blacklistedAssets.Contains(baseAsset))
                    {
                        Log.Debug($"Symbol '{symbol}' blockiert: Asset '{baseAsset}' ist geblacklisted");
                        return false;
                    }

                    // 4. Spezialprüfungen für problematische Muster

                    // Leverage-Token erkennen (enden auf Zahlen + L/S)
                    if (IsLeverageToken(baseAsset))
                    {
                        Log.Debug($"Symbol '{symbol}' blockiert: Leverage-Token erkannt");
                        return false;
                    }

                    // Sehr kurze Asset-Namen (oft Scam-Token)
                    if (baseAsset.Length <= 2 && !IsKnownShortAsset(baseAsset))
                    {
                        Log.Debug($"Symbol '{symbol}' blockiert: Verdächtig kurzer Asset-Name");
                        return false;
                    }

                    // Assets mit Zahlen (oft problematisch)
                    if (baseAsset.Any(char.IsDigit) && !IsKnownNumberAsset(baseAsset))
                    {
                        Log.Debug($"Symbol '{symbol}' blockiert: Asset enthält Zahlen");
                        return false;
                    }
                }

                return true;
            }
        }

        // Hilfsmethode: Erkennt Leverage-Token
        private bool IsLeverageToken(string asset)
        {
            if (asset.Length < 2) return false;

            // Muster: Asset endet auf [Zahl][L/S] (z.B. BTC3L, ETH5S)
            var lastChar = asset[asset.Length - 1];
            if (lastChar != 'L' && lastChar != 'S') return false;

            var secondLastChar = asset[asset.Length - 2];
            return char.IsDigit(secondLastChar);
        }

        // Hilfsmethode: Bekannte kurze Assets (Whitelist für Ausnahmen)
        private bool IsKnownShortAsset(string asset)
        {
            var knownShortAssets = new HashSet<string> { "BTC", "ETH", "BNB", "ADA", "DOT", "SOL", "XRP" };
            return knownShortAssets.Contains(asset);
        }

        // Hilfsmethode: Bekannte Assets mit Zahlen (Whitelist für Ausnahmen)
        private bool IsKnownNumberAsset(string asset)
        {
            var knownNumberAssets = new HashSet<string> { "1INCH", "0X" }; // Erweitern nach Bedarf
            return knownNumberAssets.Contains(asset);
        }

        // Filter für Listen von Symbolen
        public List<string> FilterAllowedSymbols(IEnumerable<string> symbols)
        {
            return symbols.Where(IsSymbolAllowed).ToList();
        }

        // Status-Information
        public (int BlacklistedAssets, int BlacklistedSymbols, List<string> Assets, List<string> Symbols) GetBlacklistStatus()
        {
            lock (_lock)
            {
                return (
                    _blacklistedAssets.Count,
                    _blacklistedSymbols.Count,
                    _blacklistedAssets.ToList(),
                    _blacklistedSymbols.ToList()
                );
            }
        }

        // Blacklist-Status loggen
        public void LogBlacklistStatus()
        {
            var status = GetBlacklistStatus();

            Log.Information("=== TRADING BLACKLIST STATUS ===");
            Log.Information($"Geblacklistete Assets: {status.BlacklistedAssets}");
            Log.Information($"Geblacklistete Symbole: {status.BlacklistedSymbols}");

            if (status.Assets.Any())
            {
                Log.Information($"Assets: {string.Join(", ", status.Assets)}");
            }

            if (status.Symbols.Any())
            {
                Log.Information($"Symbole: {string.Join(", ", status.Symbols)}");
            }
        }
    }
}