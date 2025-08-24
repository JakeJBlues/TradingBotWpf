using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using Serilog;

namespace TradingBotWPF.Entities
{
    /// <summary>
    /// Definiert eine Kategorie für Trading-Symbole mit spezifischen Risiko- und Positionsregeln
    /// </summary>
    public class SymbolCategory
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int MaxPositions { get; set; } = 3;
        public decimal MaxInvestmentPerPosition { get; set; } = 100m;
        public decimal TotalMaxInvestment { get; set; } = 300m;
        public decimal RiskMultiplier { get; set; } = 1.0m; // 1.0 = Normal, >1.0 = Höheres Risiko
        public decimal ProfitTarget { get; set; } = 1.5m; // 1.5% Standard-Gewinnziel
        public decimal StopLoss { get; set; } = -8.0m; // -8% Standard-Verlustgrenze
        public bool EnableAverageDown { get; set; } = true;
        public int MaxAverageDownCount { get; set; } = 3;
        public bool IsEnabled { get; set; } = true;

        // Filter-Einstellungen pro Kategorie
        public bool UseThirtyDayHighFilter { get; set; } = true;
        public decimal ThirtyDayHighThreshold { get; set; } = 0.9m;
        public bool UseRsiFilter { get; set; } = true;
        public decimal RsiThreshold { get; set; } = 70m;

        // Zeitbasierte Einstellungen
        public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromMinutes(30);
        public List<string> Symbols { get; set; } = new List<string>();

        public SymbolCategory() { }

        public SymbolCategory(string name, string description = "")
        {
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// Konfigurationsklasse für alle Symbol-Kategorien
    /// </summary>
    public class SymbolCategoryConfiguration
    {
        public List<SymbolCategory> Categories { get; set; } = new List<SymbolCategory>();
        public Dictionary<string, string> SymbolToCategoryMap { get; set; } = new Dictionary<string, string>();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Erstellt Standard-Kategorien für Kryptowährungen
        /// </summary>
        public static SymbolCategoryConfiguration CreateDefault()
        {
            var config = new SymbolCategoryConfiguration();

            // High-Cap Coins (Sichere, etablierte Coins)
            var highCap = new SymbolCategory("HIGH_CAP", "Etablierte Kryptowährungen mit hoher Marktkapitalisierung")
            {
                MaxPositions = 5,
                MaxInvestmentPerPosition = 20m,
                TotalMaxInvestment = 60m,
                RiskMultiplier = 0.8m,
                ProfitTarget = 1.2m,
                StopLoss = -5.0m,
                ThirtyDayHighThreshold = 0.95m,
                RsiThreshold = 75m,
                CooldownPeriod = TimeSpan.FromMinutes(15)
            };
            highCap.Symbols.AddRange(new[] { "BTC-EUR", "ETH-EUR", "BNB-EUR", "ADA-EUR", "DOT-EUR" });

            // Mid-Cap Coins (Moderate Risiko/Rendite)
            var midCap = new SymbolCategory("MID_CAP", "Mittlere Marktkapitalisierung mit moderatem Risiko")
            {
                MaxPositions = 4,
                MaxInvestmentPerPosition = 15m,
                TotalMaxInvestment = 50m,
                RiskMultiplier = 1.0m,
                ProfitTarget = 1.5m,
                StopLoss = -7.0m,
                ThirtyDayHighThreshold = 0.90m,
                RsiThreshold = 70m,
                CooldownPeriod = TimeSpan.FromMinutes(20)
            };
            midCap.Symbols.AddRange(new[] { "MATIC-EUR", "LINK-EUR", "UNI-EUR", "AAVE-EUR", "ALGO-EUR" });

            // Low-Cap/Alt Coins (Hohes Risiko/Rendite)
            var lowCap = new SymbolCategory("LOW_CAP", "Kleine Marktkapitalisierung mit hohem Risiko/Rendite-Potenzial")
            {
                MaxPositions = 3,
                MaxInvestmentPerPosition = 10m,
                TotalMaxInvestment = 40m,
                RiskMultiplier = 1.5m,
                ProfitTarget = 2.5m,
                StopLoss = -12.0m,
                ThirtyDayHighThreshold = 0.85m,
                RsiThreshold = 65m,
                CooldownPeriod = TimeSpan.FromMinutes(45)
            };
            lowCap.Symbols.AddRange(new[] { "SHIB-EUR", "DOGE-EUR", "LTC-EUR", "XRP-EUR", "TRX-EUR" });

            // DeFi Kategorie
            var defi = new SymbolCategory("DEFI", "DeFi-Protokolle und dezentrale Finanzanwendungen")
            {
                MaxPositions = 2,
                MaxInvestmentPerPosition = 7.5m,
                TotalMaxInvestment = 30m,
                RiskMultiplier = 1.3m,
                ProfitTarget = 2.0m,
                StopLoss = -10.0m,
                ThirtyDayHighThreshold = 0.88m,
                RsiThreshold = 68m,
                CooldownPeriod = TimeSpan.FromMinutes(30)
            };
            defi.Symbols.AddRange(new[] { "COMP-EUR", "MKR-EUR", "SNX-EUR", "CRV-EUR", "YFI-EUR" });

            // Gaming/NFT Kategorie
            var gaming = new SymbolCategory("GAMING_NFT", "Gaming und NFT-basierte Projekte")
            {
                MaxPositions = 2,
                MaxInvestmentPerPosition = 7.5m,
                TotalMaxInvestment = 30m,
                RiskMultiplier = 1.3m,
                ProfitTarget = 2.0m,
                StopLoss = -10.0m,
                ThirtyDayHighThreshold = 0.88m,
                RsiThreshold = 68m,
                CooldownPeriod = TimeSpan.FromMinutes(30)
            };
            gaming.Symbols.AddRange(new[] { "MANA-EUR", "SAND-EUR", "AXS-EUR", "ENJ-EUR" });

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
    }

    /// <summary>
    /// Manager für Symbol-Kategorien mit Positionslimits und Risikomanagement
    /// </summary>
    public class SymbolCategoryManager
    {
        private SymbolCategoryConfiguration _configuration;
        private readonly string _configPath;
        private readonly Dictionary<string, int> _currentPositionCounts;
        private readonly Dictionary<string, decimal> _currentInvestments;

        public SymbolCategoryManager(string configPath = "symbol_categories.json")
        {
            _configPath = configPath;
            _currentPositionCounts = new Dictionary<string, int>();
            _currentInvestments = new Dictionary<string, decimal>();

            LoadConfiguration();
        }

        /// <summary>
        /// Lädt die Kategorie-Konfiguration aus einer Datei oder erstellt eine Standard-Konfiguration
        /// </summary>
        public void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    _configuration = JsonSerializer.Deserialize<SymbolCategoryConfiguration>(json)
                                   ?? SymbolCategoryConfiguration.CreateDefault();
                    Log.Information($"✅ Symbol-Kategorien aus {_configPath} geladen");
                }
                else
                {
                    _configuration = SymbolCategoryConfiguration.CreateDefault();
                    SaveConfiguration();
                    Log.Information($"📁 Standard-Kategorien erstellt und in {_configPath} gespeichert");
                }

                LogCategoryOverview();
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"❌ Fehler beim Laden der Kategorien-Konfiguration");
                _configuration = SymbolCategoryConfiguration.CreateDefault();
            }
        }

        /// <summary>
        /// Speichert die aktuelle Konfiguration in eine Datei
        /// </summary>
        public void SaveConfiguration()
        {
            try
            {
                _configuration.LastUpdated = DateTime.UtcNow;
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_configuration, options);
                File.WriteAllText(_configPath, json);
                Log.Information($"💾 Symbol-Kategorien in {_configPath} gespeichert");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"❌ Fehler beim Speichern der Kategorien-Konfiguration");
            }
        }

        /// <summary>
        /// Fügt ein Symbol zu einer Kategorie hinzu
        /// </summary>
        public bool AddSymbolToCategory(string symbol, string categoryName)
        {
            var category = _configuration.Categories.FirstOrDefault(c => c.Name == categoryName);
            if (category == null)
            {
                Log.Warning($"⚠️ Kategorie '{categoryName}' nicht gefunden");
                return false;
            }

            // Entferne Symbol aus anderen Kategorien
            RemoveSymbolFromAllCategories(symbol);

            if (!category.Symbols.Contains(symbol))
            {
                category.Symbols.Add(symbol);
                _configuration.SymbolToCategoryMap[symbol] = categoryName;

                Log.Information($"✅ Symbol '{symbol}' zu Kategorie '{categoryName}' hinzugefügt");
                SaveConfiguration();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Entfernt ein Symbol aus allen Kategorien
        /// </summary>
        public void RemoveSymbolFromAllCategories(string symbol)
        {
            foreach (var category in _configuration.Categories)
            {
                if (category.Symbols.Remove(symbol))
                {
                    Log.Information($"🗑️ Symbol '{symbol}' aus Kategorie '{category.Name}' entfernt");
                }
            }
            _configuration.SymbolToCategoryMap.Remove(symbol);
        }

        /// <summary>
        /// Gibt die Kategorie für ein Symbol zurück
        /// </summary>
        public SymbolCategory? GetCategoryForSymbol(string symbol)
        {
            if (_configuration.SymbolToCategoryMap.TryGetValue(symbol, out var categoryName))
            {
                return _configuration.Categories.FirstOrDefault(c => c.Name == categoryName);
            }
            return null;
        }

        /// <summary>
        /// Prüft, ob ein neuer Kauf für ein Symbol erlaubt ist
        /// </summary>
        public (bool CanBuy, string Reason, decimal MaxInvestment) CanBuySymbol(string symbol, decimal requestedAmount)
        {
            var category = GetCategoryForSymbol(symbol);
            if (category == null)
            {
                return (false, $"Symbol '{symbol}' ist keiner Kategorie zugewiesen", 0m);
            }

            if (!category.IsEnabled)
            {
                return (false, $"Kategorie '{category.Name}' ist deaktiviert", 0m);
            }

            // Position-Limit prüfen
            var currentPositions = GetCurrentPositionCount(category.Name);
            if (currentPositions >= category.MaxPositions)
            {
                return (false, $"Positions-Limit erreicht: {currentPositions}/{category.MaxPositions} in '{category.Name}'", 0m);
            }

            // Investment-Limit pro Position prüfen
            if (requestedAmount > category.MaxInvestmentPerPosition)
            {
                return (false, $"Investment zu hoch: {requestedAmount:F2} EUR > {category.MaxInvestmentPerPosition:F2} EUR Limit", category.MaxInvestmentPerPosition);
            }

            // Gesamtinvestment-Limit prüfen
            var currentInvestment = GetCurrentTotalInvestment(category.Name);
            if (currentInvestment + requestedAmount > category.TotalMaxInvestment)
            {
                var availableAmount = category.TotalMaxInvestment - currentInvestment;
                return (false, $"Gesamtinvestment-Limit würde überschritten: {availableAmount:F2} EUR verfügbar in '{category.Name}'", availableAmount);
            }

            return (true, "OK", Math.Min(requestedAmount, category.MaxInvestmentPerPosition));
        }

        /// <summary>
        /// Berechnet das empfohlene Investment basierend auf dem Risikomultiplikator
        /// </summary>
        public decimal CalculateRecommendedInvestment(string symbol, decimal baseAmount)
        {
            var category = GetCategoryForSymbol(symbol);
            if (category == null) return baseAmount;

            var adjustedAmount = baseAmount * category.RiskMultiplier;
            return Math.Min(adjustedAmount, category.MaxInvestmentPerPosition);
        }

        /// <summary>
        /// Registriert eine neue Position für Kategorie-Tracking
        /// </summary>
        public void RegisterPosition(string symbol, decimal investmentAmount)
        {
            var category = GetCategoryForSymbol(symbol);
            if (category == null) return;

            var categoryName = category.Name;

            if (!_currentPositionCounts.ContainsKey(categoryName))
                _currentPositionCounts[categoryName] = 0;
            if (!_currentInvestments.ContainsKey(categoryName))
                _currentInvestments[categoryName] = 0m;

            _currentPositionCounts[categoryName]++;
            _currentInvestments[categoryName] += investmentAmount;

            Log.Information($"📊 Position registriert: {symbol} | Kategorie: {categoryName} | Positionen: {_currentPositionCounts[categoryName]}/{category.MaxPositions} | Investment: {_currentInvestments[categoryName]:F2}/{category.TotalMaxInvestment:F2} EUR");
        }

        /// <summary>
        /// Entfernt eine Position aus dem Kategorie-Tracking
        /// </summary>
        public void UnregisterPosition(string symbol, decimal investmentAmount)
        {
            var category = GetCategoryForSymbol(symbol);
            if (category == null) return;

            var categoryName = category.Name;

            if (_currentPositionCounts.ContainsKey(categoryName))
            {
                _currentPositionCounts[categoryName] = Math.Max(0, _currentPositionCounts[categoryName] - 1);
            }

            if (_currentInvestments.ContainsKey(categoryName))
            {
                _currentInvestments[categoryName] = Math.Max(0m, _currentInvestments[categoryName] - investmentAmount);
            }

            Log.Information($"📉 Position entfernt: {symbol} | Kategorie: {categoryName} | Verbleibende Positionen: {_currentPositionCounts.GetValueOrDefault(categoryName, 0)}");
        }

        /// <summary>
        /// Gibt alle verfügbaren Kategorien zurück
        /// </summary>
        public List<SymbolCategory> GetAllCategories()
        {
            return _configuration.Categories.ToList();
        }

        /// <summary>
        /// Gibt eine spezifische Kategorie zurück
        /// </summary>
        public SymbolCategory? GetCategory(string categoryName)
        {
            return _configuration.Categories.FirstOrDefault(c => c.Name == categoryName);
        }

        /// <summary>
        /// Aktualisiert die Eigenschaften einer Kategorie
        /// </summary>
        public bool UpdateCategory(string categoryName, Action<SymbolCategory> updateAction)
        {
            var category = GetCategory(categoryName);
            if (category == null) return false;

            updateAction(category);
            SaveConfiguration();

            Log.Information($"✅ Kategorie '{categoryName}' aktualisiert");
            return true;
        }

        /// <summary>
        /// Erstellt eine neue Kategorie
        /// </summary>
        public bool CreateCategory(SymbolCategory newCategory)
        {
            if (_configuration.Categories.Any(c => c.Name == newCategory.Name))
            {
                Log.Warning($"⚠️ Kategorie '{newCategory.Name}' existiert bereits");
                return false;
            }

            _configuration.Categories.Add(newCategory);
            SaveConfiguration();

            Log.Information($"✅ Neue Kategorie '{newCategory.Name}' erstellt");
            return true;
        }

        /// <summary>
        /// Gibt Symbole zurück, die für das Trading in Frage kommen (gefiltert nach Kategorien)
        /// </summary>
        public List<string> GetTradableSymbols(List<string> allSymbols)
        {
            return allSymbols.Where(symbol =>
            {
                var category = GetCategoryForSymbol(symbol);
                return category != null && category.IsEnabled;
            }).ToList();
        }

        /// <summary>
        /// Gibt den aktuellen Status aller Kategorien zurück
        /// </summary>
        public Dictionary<string, (int CurrentPositions, int MaxPositions, decimal CurrentInvestment, decimal MaxInvestment)> GetCategoryStatus()
        {
            var status = new Dictionary<string, (int, int, decimal, decimal)>();

            foreach (var category in _configuration.Categories)
            {
                var currentPositions = GetCurrentPositionCount(category.Name);
                var currentInvestment = GetCurrentTotalInvestment(category.Name);

                status[category.Name] = (currentPositions, category.MaxPositions, currentInvestment, category.TotalMaxInvestment);
            }

            return status;
        }

        private int GetCurrentPositionCount(string categoryName)
        {
            return _currentPositionCounts.GetValueOrDefault(categoryName, 0);
        }

        private decimal GetCurrentTotalInvestment(string categoryName)
        {
            return _currentInvestments.GetValueOrDefault(categoryName, 0m);
        }

        private void LogCategoryOverview()
        {
            Log.Information("=== 📊 SYMBOL-KATEGORIEN ÜBERSICHT ===");
            foreach (var category in _configuration.Categories)
            {
                var status = category.IsEnabled ? "✅" : "❌";
                Log.Information($"{status} {category.Name}: {category.Symbols.Count} Symbole | Max: {category.MaxPositions} Pos, {category.TotalMaxInvestment:F0} EUR | Risiko: {category.RiskMultiplier:F1}x");
            }
            Log.Information($"📁 Gesamt: {_configuration.Categories.Count} Kategorien, {_configuration.SymbolToCategoryMap.Count} zugewiesene Symbole");
        }

        /// <summary>
        /// Zeigt detaillierte Kategorie-Statistiken
        /// </summary>
        public void LogDetailedCategoryStatus()
        {
            Log.Information("=== 📈 DETAILLIERTE KATEGORIE-STATISTIKEN ===");

            foreach (var category in _configuration.Categories.Where(c => c.IsEnabled))
            {
                var currentPositions = GetCurrentPositionCount(category.Name);
                var currentInvestment = GetCurrentTotalInvestment(category.Name);
                var utilizationPercent = currentInvestment / category.TotalMaxInvestment * 100;

                Log.Information($"📊 {category.Name}:");
                Log.Information($"   Positionen: {currentPositions}/{category.MaxPositions} ({(double)currentPositions / category.MaxPositions * 100:F0}%)");
                Log.Information($"   Investment: {currentInvestment:F2}/{category.TotalMaxInvestment:F2} EUR ({utilizationPercent:F1}%)");
                Log.Information($"   Risiko: {category.RiskMultiplier:F1}x | Profit-Ziel: {category.ProfitTarget:F1}% | Stop-Loss: {category.StopLoss:F1}%");
                Log.Information($"   Filter: 30T-High({(category.UseThirtyDayHighFilter ? $"{category.ThirtyDayHighThreshold * 100:F0}%" : "AUS")}) | RSI({(category.UseRsiFilter ? $"{category.RsiThreshold}" : "AUS")})");

                if (category.Symbols.Any())
                {
                    Log.Information($"   Symbole: {string.Join(", ", category.Symbols.Take(5))}{(category.Symbols.Count > 5 ? $" +{category.Symbols.Count - 5} weitere" : "")}");
                }
            }
        }
    }
}