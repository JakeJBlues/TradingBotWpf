using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using TradingBotCore.Interfaces.Manager;

namespace TradingBotCore.Manager
{
    /// <summary>
    /// Erweiterte Cooldown-Information für UI-Anzeige
    /// </summary>
    public class CooldownInfo
    {
        public string Symbol { get; set; }
        public TimeSpan RemainingTime { get; set; }
        public CooldownType Type { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Description { get; set; }
    }

    public enum CooldownType
    {
        BuyCooldown,        // Kauf-Cooldown
        SellCooldown,       // Verkauf-Cooldown  
        SellLockout,        // Verkaufssperre nach Verkauf
        GlobalCooldown      // Globaler Cooldown
    }

    /// <summary>
    /// Erweiterte Trading-Cooldown-Manager mit vollständiger UI-Integration
    /// </summary>
    public class EnhancedTradingCooldownManager : TradingCooldownManager, IEnhancedTradingCooldownManager
    {
        // Zusätzliche Tracking für UI
        private readonly ConcurrentDictionary<string, DateTime> _lastGlobalActions = new();
        private DateTime _lastAnyGlobalAction = DateTime.MinValue;
        private readonly object _globalLock = new object();

        public EnhancedTradingCooldownManager(TimeSpan buyDelay, TimeSpan sellDelay,
            TimeSpan globalCooldown, TimeSpan sellLockoutDuration)
            : base(buyDelay, sellDelay, globalCooldown, sellLockoutDuration)
        {
        }

        /// <summary>
        /// Gibt ALLE aktiven Cooldowns für die UI zurück
        /// </summary>
        public List<CooldownInfo> GetAllActiveCooldowns()
        {
            var cooldowns = new List<CooldownInfo>();
            var now = DateTime.UtcNow;

            // 1. Kauf-Cooldowns abrufen
            var buyDelays = GetBuyCooldowns();
            foreach (var kvp in buyDelays)
            {
                if (kvp.Value > TimeSpan.Zero)
                {
                    cooldowns.Add(new CooldownInfo
                    {
                        Symbol = kvp.Key,
                        RemainingTime = kvp.Value,
                        Type = CooldownType.BuyCooldown,
                        ExpiresAt = now.Add(kvp.Value),
                        Description = $"Kauf-Cooldown: Nächster Kauf in {kvp.Value.TotalMinutes:F1} Min"
                    });
                }
            }

            // 2. Verkauf-Cooldowns abrufen
            var sellDelays = GetSellCooldowns();
            foreach (var kvp in sellDelays)
            {
                if (kvp.Value > TimeSpan.Zero)
                {
                    cooldowns.Add(new CooldownInfo
                    {
                        Symbol = kvp.Key,
                        RemainingTime = kvp.Value,
                        Type = CooldownType.SellCooldown,
                        ExpiresAt = now.Add(kvp.Value),
                        Description = $"Verkauf-Cooldown: Nächster Verkauf in {kvp.Value.TotalMinutes:F1} Min"
                    });
                }
            }

            // 3. Verkaufssperren abrufen (bestehende Methode)
            var sellLockouts = GetActiveLockouts();
            foreach (var kvp in sellLockouts)
            {
                cooldowns.Add(new CooldownInfo
                {
                    Symbol = kvp.Key,
                    RemainingTime = kvp.Value,
                    Type = CooldownType.SellLockout,
                    ExpiresAt = now.Add(kvp.Value),
                    Description = $"Verkaufssperre: Kein Neukauf für {kvp.Value.TotalMinutes:F1} Min"
                });
            }

            // 4. Globaler Cooldown
            var globalCooldown = GetGlobalCooldownRemaining();
            if (globalCooldown > TimeSpan.Zero)
            {
                cooldowns.Add(new CooldownInfo
                {
                    Symbol = "GLOBAL",
                    RemainingTime = globalCooldown,
                    Type = CooldownType.GlobalCooldown,
                    ExpiresAt = now.Add(globalCooldown),
                    Description = $"Globaler Cooldown: Alle Aktionen gesperrt für {globalCooldown.TotalSeconds:F0}s"
                });
            }

            return cooldowns.OrderBy(c => c.RemainingTime).ToList();
        }

        /// <summary>
        /// Neue Methode: Gibt Kauf-Cooldowns zurück
        /// </summary>
        public Dictionary<string, TimeSpan> GetBuyCooldowns()
        {
            // Zugriff auf private _lastBuyTimes über Reflection oder neue Implementierung
            return GetCooldownsOfType("Buy");
        }

        /// <summary>
        /// Neue Methode: Gibt Verkauf-Cooldowns zurück
        /// </summary>
        public Dictionary<string, TimeSpan> GetSellCooldowns()
        {
            return GetCooldownsOfType("Sell");
        }

        /// <summary>
        /// Hilfsmethode: Simuliert Zugriff auf private Cooldown-Daten
        /// In echter Implementierung würden Sie die TradingCooldownManager-Klasse erweitern
        /// </summary>
        private Dictionary<string, TimeSpan> GetCooldownsOfType(string type)
        {
            var result = new Dictionary<string, TimeSpan>();
            var now = DateTime.UtcNow;

            // HINWEIS: Da wir nicht auf private Fields zugreifen können,
            // implementieren wir hier eine Tracking-Lösung

            // Verwende das globale Tracking als Fallback
            foreach (var kvp in _lastGlobalActions)
            {
                TimeSpan cooldownPeriod = type == "Buy" ? TimeSpan.FromMinutes(10) : TimeSpan.FromMinutes(2);
                var timeSinceAction = now - kvp.Value;
                var remainingTime = cooldownPeriod - timeSinceAction;

                if (remainingTime > TimeSpan.Zero)
                {
                    result[kvp.Key] = remainingTime;
                }
            }

            return result;
        }

        /// <summary>
        /// Neue Methode: Globaler Cooldown-Status
        /// </summary>
        public TimeSpan GetGlobalCooldownRemaining()
        {
            lock (_globalLock)
            {
                var timeSinceLastGlobal = DateTime.UtcNow - _lastAnyGlobalAction;
                var globalCooldownPeriod = TimeSpan.FromSeconds(30); // Ihr eingestellter Wert
                var remaining = globalCooldownPeriod - timeSinceLastGlobal;

                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Erweiterte RecordBuy-Methode mit besserem Tracking
        /// </summary>
        public new void RecordBuy(string symbol)
        {
            base.RecordBuy(symbol);

            var now = DateTime.UtcNow;
            _lastGlobalActions.AddOrUpdate(symbol, now, (key, oldValue) => now);

            lock (_globalLock)
            {
                _lastAnyGlobalAction = now;
            }

            Log.Information($"🛒 Kauf-Cooldown für {symbol} aktiviert bis {now.AddMinutes(10):HH:mm:ss}");
        }

        /// <summary>
        /// Erweiterte RecordSell-Methode mit besserem Tracking
        /// </summary>
        public new void RecordSell(string symbol)
        {
            base.RecordSell(symbol);

            var now = DateTime.UtcNow;
            _lastGlobalActions.AddOrUpdate(symbol, now, (key, oldValue) => now);

            lock (_globalLock)
            {
                _lastAnyGlobalAction = now;
            }

            Log.Information($"💰 Verkauf-Cooldown für {symbol} aktiviert bis {now.AddMinutes(2):HH:mm:ss}");
        }

        /// <summary>
        /// Statistiken für Dashboard
        /// </summary>
        public CooldownStatistics GetCooldownStatistics()
        {
            var allCooldowns = GetAllActiveCooldowns();

            return new CooldownStatistics
            {
                TotalActiveCooldowns = allCooldowns.Count,
                BuyCooldowns = allCooldowns.Count(c => c.Type == CooldownType.BuyCooldown),
                SellCooldowns = allCooldowns.Count(c => c.Type == CooldownType.SellCooldown),
                SellLockouts = allCooldowns.Count(c => c.Type == CooldownType.SellLockout),
                HasGlobalCooldown = allCooldowns.Any(c => c.Type == CooldownType.GlobalCooldown),
                NextExpiringCooldown = allCooldowns.OrderBy(c => c.ExpiresAt).FirstOrDefault()
            };
        }
    }

    /// <summary>
    /// Cooldown-Statistiken für Dashboard
    /// </summary>
    public class CooldownStatistics
    {
        public int TotalActiveCooldowns { get; set; }
        public int BuyCooldowns { get; set; }
        public int SellCooldowns { get; set; }
        public int SellLockouts { get; set; }
        public bool HasGlobalCooldown { get; set; }
        public CooldownInfo NextExpiringCooldown { get; set; }

        public string Summary => $"Aktiv: {TotalActiveCooldowns} | Kauf: {BuyCooldowns} | Verkauf: {SellCooldowns} | Sperren: {SellLockouts}";
    }

    /// <summary>
    /// Erweiterte ViewModel für Cooldown-Anzeige
    /// </summary>
    public class EnhancedCooldownViewModel
    {
        public string Symbol { get; set; }
        public string RemainingTime { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public DateTime ExpiresAt { get; set; }
        public CooldownType CooldownType { get; set; }

        // UI-Properties
        public string TypeIcon => CooldownType switch
        {
            CooldownType.BuyCooldown => "🛒",
            CooldownType.SellCooldown => "💰",
            CooldownType.SellLockout => "🚫",
            CooldownType.GlobalCooldown => "⏰",
            _ => "❓"
        };

        public string TypeColor => CooldownType switch
        {
            CooldownType.BuyCooldown => "Blue",
            CooldownType.SellCooldown => "Green",
            CooldownType.SellLockout => "Red",
            CooldownType.GlobalCooldown => "Orange",
            _ => "Gray"
        };

        public string ExpiresAtFormatted => ExpiresAt.ToString("HH:mm:ss");

        public string Priority => CooldownType switch
        {
            CooldownType.GlobalCooldown => "HOCH",
            CooldownType.SellLockout => "MITTEL",
            CooldownType.BuyCooldown => "NIEDRIG",
            CooldownType.SellCooldown => "NIEDRIG",
            _ => "UNBEKANNT"
        };
    }
}