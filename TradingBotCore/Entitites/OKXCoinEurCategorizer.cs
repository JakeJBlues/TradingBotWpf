using System;
using System.Collections.Generic;
using System.Linq;

namespace TradingBotCore.Entities
{
    /// <summary>
    /// Kategorisiert OKX EUR-Coins in die vorhandenen SymbolCategory-Kategorien
    /// </summary>
    public static class OKXEurCoinCategorizer
    {
        /// <summary>
        /// Erstellt eine aktualisierte Kategorie-Konfiguration mit allen verfügbaren OKX EUR-Coins
        /// </summary>
        public static SymbolCategoryConfiguration CreateOKXEurConfiguration()
        {
            var config = new SymbolCategoryConfiguration();

            // HIGH_CAP - Etablierte Kryptowährungen mit hoher Marktkapitalisierung
            var highCap = new SymbolCategory("HIGH_CAP", "Etablierte Kryptowährungen mit hoher Marktkapitalisierung")
            {
                MaxPositions = 5,
                MaxInvestmentPerPosition = 200m,
                TotalMaxInvestment = 800m,
                RiskMultiplier = 0.8m,
                ProfitTarget = 1.2m,
                StopLoss = -5.0m,
                ThirtyDayHighThreshold = 0.95m,
                RsiThreshold = 75m,
                CooldownPeriod = TimeSpan.FromMinutes(15)
            };
            highCap.Symbols.AddRange(GetHighCapCoins());

            // MID_CAP - Mittlere Marktkapitalisierung mit moderatem Risiko
            var midCap = new SymbolCategory("MID_CAP", "Mittlere Marktkapitalisierung mit moderatem Risiko")
            {
                MaxPositions = 4,
                MaxInvestmentPerPosition = 150m,
                TotalMaxInvestment = 600m,
                RiskMultiplier = 1.0m,
                ProfitTarget = 1.5m,
                StopLoss = -7.0m,
                ThirtyDayHighThreshold = 0.90m,
                RsiThreshold = 70m,
                CooldownPeriod = TimeSpan.FromMinutes(20)
            };
            midCap.Symbols.AddRange(GetMidCapCoins());

            // LOW_CAP - Kleine Marktkapitalisierung mit hohem Risiko/Rendite-Potenzial
            var lowCap = new SymbolCategory("LOW_CAP", "Kleine Marktkapitalisierung mit hohem Risiko/Rendite-Potenzial")
            {
                MaxPositions = 3,
                MaxInvestmentPerPosition = 100m,
                TotalMaxInvestment = 300m,
                RiskMultiplier = 1.5m,
                ProfitTarget = 2.5m,
                StopLoss = -12.0m,
                ThirtyDayHighThreshold = 0.85m,
                RsiThreshold = 65m,
                CooldownPeriod = TimeSpan.FromMinutes(45)
            };
            lowCap.Symbols.AddRange(GetLowCapCoins());

            // DEFI - DeFi-Protokolle und dezentrale Finanzanwendungen
            var defi = new SymbolCategory("DEFI", "DeFi-Protokolle und dezentrale Finanzanwendungen")
            {
                MaxPositions = 2,
                MaxInvestmentPerPosition = 120m,
                TotalMaxInvestment = 240m,
                RiskMultiplier = 1.3m,
                ProfitTarget = 2.0m,
                StopLoss = -10.0m,
                ThirtyDayHighThreshold = 0.88m,
                RsiThreshold = 68m,
                CooldownPeriod = TimeSpan.FromMinutes(30)
            };
            defi.Symbols.AddRange(GetDefiCoins());

            // GAMING_NFT - Gaming und NFT-basierte Projekte
            var gaming = new SymbolCategory("GAMING_NFT", "Gaming und NFT-basierte Projekte")
            {
                MaxPositions = 2,
                MaxInvestmentPerPosition = 80m,
                TotalMaxInvestment = 160m,
                RiskMultiplier = 1.8m,
                ProfitTarget = 3.0m,
                StopLoss = -15.0m,
                ThirtyDayHighThreshold = 0.80m,
                RsiThreshold = 60m,
                CooldownPeriod = TimeSpan.FromHours(1),
                EnableAverageDown = false
            };
            gaming.Symbols.AddRange(GetGamingNftCoins());

            config.Categories.AddRange(new[] { highCap, midCap, lowCap, defi, gaming });

            // Symbol-zu-Kategorie Mapping erstellen
            foreach (var category in config.Categories)
            {
                foreach (var symbol in category.Symbols)
                {
                    config.SymbolToCategoryMap[symbol] = category.Name;
                }
            }

            return config;
        }

        /// <summary>
        /// High-Cap Coins (Marktkapitalisierung > 20 Milliarden USD)
        /// </summary>
        private static List<string> GetHighCapCoins()
        {
            return new List<string>
            {
                // Top Tier Kryptowährungen
                "BTC-EUR",    // Bitcoin
                "ETH-EUR",    // Ethereum
                "BNB-EUR",    // Binance Coin
                "XRP-EUR",    // Ripple
                "ADA-EUR",    // Cardano
                "SOL-EUR",    // Solana
                "DOT-EUR",    // Polkadot
                "AVAX-EUR",   // Avalanche
                "MATIC-EUR",  // Polygon
                "TRX-EUR",    // Tron
                "TON-EUR",    // Toncoin
                "ICP-EUR",    // Internet Computer
                "NEAR-EUR",   // Near Protocol
                "ATOM-EUR",   // Cosmos
                "LTC-EUR"     // Litecoin
            };
        }

        /// <summary>
        /// Mid-Cap Coins (Marktkapitalisierung 1-20 Milliarden USD)
        /// </summary>
        private static List<string> GetMidCapCoins()
        {
            return new List<string>
            {
                // Smart Contract Platforms
                "LINK-EUR",   // Chainlink
                "ALGO-EUR",   // Algorand
                "VET-EUR",    // VeChain
                "FTM-EUR",    // Fantom
                "ONE-EUR",    // Harmony
                "HBAR-EUR",   // Hedera
                "FLOW-EUR",   // Flow
                "EGLD-EUR",   // MultiversX
                "ROSE-EUR",   // Oasis Network
                "KSM-EUR",    // Kusama
                
                // Infrastructure
                "FIL-EUR",    // Filecoin
                "GRT-EUR",    // The Graph
                "AR-EUR",     // Arweave
                "THETA-EUR",  // Theta Network
                
                // Enterprise & Payments
                "XLM-EUR",    // Stellar
                "XTZ-EUR",    // Tezos
                "MINA-EUR",   // Mina Protocol
                "IOTA-EUR",   // IOTA
                "WAVES-EUR",  // Waves
                "ZIL-EUR"     // Zilliqa
            };
        }

        /// <summary>
        /// Low-Cap Coins (Marktkapitalisierung 100M-1 Milliarde USD + Meme Coins)
        /// </summary>
        private static List<string> GetLowCapCoins()
        {
            return new List<string>
            {
                // Meme Coins (hohe Volatilität)
                "DOGE-EUR",   // Dogecoin
                "SHIB-EUR",   // Shiba Inu
                "PEPE-EUR",   // Pepe
                "FLOKI-EUR",  // Floki
                "BONK-EUR",   // Bonk
                
                // Low-Cap Altcoins
                "JASMY-EUR",  // JasmyCoin
                "CHZ-EUR",    // Chiliz
                "HOT-EUR",    // Holo
                "WIN-EUR",    // WINkLink
                "BTT-EUR",    // BitTorrent
                "LUNA-EUR",   // Terra Luna Classic
                "LUNC-EUR",   // Terra Luna Classic
                "USTC-EUR",   // TerraClassicUSD
                
                // Privacy & Utility
                "ZEC-EUR",    // Zcash
                "DASH-EUR",   // Dash
                "DCR-EUR",    // Decred
                "DGB-EUR",    // DigiByte
                "RVN-EUR",    // Ravencoin
                "SC-EUR",     // Siacoin
                
                // Emerging Projects
                "CELO-EUR",   // Celo
                "1INCH-EUR",  // 1inch
                "RSR-EUR",    // Reserve Rights
                "ANKR-EUR",   // Ankr
                "STORJ-EUR",  // Storj
                "OCEAN-EUR",  // Ocean Protocol
                "NKN-EUR",    // NKN
                "BAND-EUR",   // Band Protocol
                "API3-EUR",   // API3
                "REN-EUR"     // Ren
            };
        }

        /// <summary>
        /// DeFi Coins (DeFi-Protokolle und dezentrale Finanzanwendungen)
        /// </summary>
        private static List<string> GetDefiCoins()
        {
            return new List<string>
            {
                // DEX Protokolle
                "UNI-EUR",    // Uniswap
                "SUSHI-EUR",  // SushiSwap
                "CAKE-EUR",   // PancakeSwap
                "CRV-EUR",    // Curve DAO
                "BAL-EUR",    // Balancer
                
                // Lending & Borrowing
                "AAVE-EUR",   // Aave
                "COMP-EUR",   // Compound
                "MKR-EUR",    // MakerDAO
                "SNX-EUR",    // Synthetix
                "YFI-EUR",    // yearn.finance
                
                // Yield Farming
                "ALPHA-EUR",  // Alpha Finance
                "AUTO-EUR",   // Auto
                "BIFI-EUR",   // Beefy Finance
                
                // Insurance & Derivatives
                "CVX-EUR",    // Convex Finance
                "LDO-EUR",    // Lido DAO
                "RPL-EUR",    // Rocket Pool
                
                // Cross-Chain DeFi
                "RUNE-EUR",   // THORChain
                "KAVA-EUR",   // Kava
                "INJ-EUR",    // Injective
                "OSMO-EUR",   // Osmosis
                "JUNO-EUR"    // Juno Network
            };
        }

        /// <summary>
        /// Gaming & NFT Coins (Gaming, Metaverse und NFT-Projekte)
        /// </summary>
        private static List<string> GetGamingNftCoins()
        {
            return new List<string>
            {
                // Metaverse Platforms
                "MANA-EUR",   // Decentraland
                "SAND-EUR",   // The Sandbox
                "ENJ-EUR",    // Enjin Coin
                "GALA-EUR",   // Gala
                "ILV-EUR",    // Illuvium
                
                // GameFi & Play-to-Earn
                "AXS-EUR",    // Axie Infinity
                "SLP-EUR",    // Smooth Love Potion
                "TLM-EUR",    // Alien Worlds
                "ALICE-EUR",  // My Neighbor Alice
                "YGG-EUR",    // Yield Guild Games
                
                // Virtual Real Estate
                "LAND-EUR",   // Landshare (falls verfügbar)
                "SUPER-EUR",  // SuperFarm
                
                // NFT Infrastructure
                "FLOW-EUR",   // Flow (auch NFT-fokussiert)
                "IMX-EUR",    // Immutable X
                "LOOKS-EUR",  // LooksRare
                
                // Sports & Entertainment
                "CHZ-EUR",    // Chiliz (auch in Low-Cap, aber Gaming-relevant)
                "AUDIO-EUR",  // Audius
                "THETA-EUR"   // Theta Network (auch Entertainment)
            };
        }

        /// <summary>
        /// Kategorisiert einen einzelnen Coin basierend auf seinem Symbol
        /// </summary>
        public static string? CategorizeSymbol(string symbol)
        {
            var config = CreateOKXEurConfiguration();
            return config.SymbolToCategoryMap.GetValueOrDefault(symbol);
        }

        /// <summary>
        /// Gibt alle verfügbaren EUR-Symbole zurück, die in Kategorien eingeordnet sind
        /// </summary>
        public static List<string> GetAllCategorizedSymbols()
        {
            var config = CreateOKXEurConfiguration();
            return config.SymbolToCategoryMap.Keys.ToList();
        }

        /// <summary>
        /// Gibt Symbole einer bestimmten Kategorie zurück
        /// </summary>
        public static List<string> GetSymbolsByCategory(string categoryName)
        {
            var config = CreateOKXEurConfiguration();
            var category = config.Categories.FirstOrDefault(c => c.Name == categoryName);
            return category?.Symbols ?? new List<string>();
        }

        /// <summary>
        /// Prüft, ob ein Symbol in einer bestimmten Kategorie ist
        /// </summary>
        public static bool IsSymbolInCategory(string symbol, string categoryName)
        {
            var config = CreateOKXEurConfiguration();
            return config.SymbolToCategoryMap.GetValueOrDefault(symbol) == categoryName;
        }

        /// <summary>
        /// Gibt Statistiken über die Kategorisierung zurück
        /// </summary>
        public static Dictionary<string, int> GetCategorizationStats()
        {
            var config = CreateOKXEurConfiguration();
            return config.Categories.ToDictionary(c => c.Name, c => c.Symbols.Count);
        }

        /// <summary>
        /// Aktualisiert eine bestehende SymbolCategoryConfiguration mit OKX EUR-Coins
        /// </summary>
        public static void UpdateExistingConfiguration(SymbolCategoryConfiguration existingConfig)
        {
            var okxConfig = CreateOKXEurConfiguration();

            // Bestehende Kategorien aktualisieren oder neue hinzufügen
            foreach (var okxCategory in okxConfig.Categories)
            {
                var existingCategory = existingConfig.Categories
                    .FirstOrDefault(c => c.Name == okxCategory.Name);

                if (existingCategory != null)
                {
                    // Symbole zur bestehenden Kategorie hinzufügen (Duplikate vermeiden)
                    foreach (var symbol in okxCategory.Symbols)
                    {
                        if (!existingCategory.Symbols.Contains(symbol))
                        {
                            existingCategory.Symbols.Add(symbol);
                        }
                    }
                }
                else
                {
                    // Neue Kategorie hinzufügen
                    existingConfig.Categories.Add(okxCategory);
                }
            }

            // Symbol-Mapping aktualisieren
            foreach (var mapping in okxConfig.SymbolToCategoryMap)
            {
                existingConfig.SymbolToCategoryMap[mapping.Key] = mapping.Value;
            }

            existingConfig.LastUpdated = DateTime.UtcNow;
        }
    }
}