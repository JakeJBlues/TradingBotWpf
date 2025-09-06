using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TradingBotCore.Interfaces;
using static TradingBotCore.Helper.ProtectedProfitTracker;

namespace TradingBotCore.Helper
{
    /// <summary>
    /// Erweiterte Profit-Tracker mit striktem Profit-Schutz
    /// </summary>
    public class ProtectedProfitTracker : ProfitTracker, IProtectedProfitTracker
    {
        private decimal _initialTradingBudget = 0;
        private decimal _maxTradingBudget = 0; // Wird niemals überschritten
        private decimal _totalRealizedProfit = 0;
        private decimal _protectedProfit = 0; // Gewinn der NICHT reinvestiert wird
        private decimal _availableTradingBudget = 0;
        private decimal _totalInvested = 0;
        private decimal _overallPL = 0;
        private readonly List<(DateTime Timestamp, decimal Profit, string Symbol, string Type)> _profitHistory = new();
        private readonly object _lock = new object();

        public enum ProfitProtectionMode
        {
            Full,           // Gesamter Gewinn wird geschützt
            Percentage,     // Nur ein % des Gewinns wird geschützt  
            Threshold       // Gewinn über einem Schwellenwert wird geschützt
        }

        private ProfitProtectionMode _protectionMode = ProfitProtectionMode.Full;
        private decimal _protectionPercentage = 80m; // 80% des Gewinns schützen
        private decimal _protectionThreshold = 50m;  // Gewinn über 50 EUR schützen

        public void SetOverAllPL(decimal profit)
        {
            _overallPL += profit;
        }

        /// <summary>
        /// Initialisiert das Trading-Budget mit striktem Profit-Schutz
        /// </summary>
        public new void SetInitialBalance(decimal balance)
        {
            lock (_lock)
            {
                if (_initialTradingBudget == 0)
                {
                    _initialTradingBudget = balance;
                    _maxTradingBudget = balance; // ✅ Maximum wird NIEMALS überschritten
                    _availableTradingBudget = balance;
                    _totalRealizedProfit = 0;
                    _protectedProfit = 0;

                    Log.Information($"=== 🛡️ PROFIT-SCHUTZ AKTIVIERT ===");
                    Log.Information($"Initial Trading-Budget: {_initialTradingBudget:F2} EUR");
                    Log.Information($"Maximum Trading-Budget: {_maxTradingBudget:F2} EUR (WIRD NIEMALS ÜBERSCHRITTEN)");
                    Log.Information($"Profit-Schutz-Modus: {_protectionMode}");

                    if (_protectionMode == ProfitProtectionMode.Percentage)
                    {
                        Log.Information($"Schutz-Prozentsatz: {_protectionPercentage}% des Gewinns");
                    }
                    else if (_protectionMode == ProfitProtectionMode.Threshold)
                    {
                        Log.Information($"Schutz-Schwellenwert: Gewinn über {_protectionThreshold:F2} EUR");
                    }
                }
            }
        }

        /// <summary>
        /// Reserviert Budget für Kauf - berücksichtigt nur ursprüngliches Budget
        /// </summary>
        public new bool ReserveBudgetForPurchase(decimal purchaseAmount, string symbol)
        {
            lock (_lock)
            {
                // ✅ Prüfe nur gegen verfügbares Trading-Budget (OHNE Gewinn)
                if (_availableTradingBudget >= purchaseAmount)
                {
                    _availableTradingBudget -= purchaseAmount;
                    _totalInvested += purchaseAmount;

                    Log.Information($"💰 Budget reserviert für {symbol}: {purchaseAmount:F2} EUR");
                    Log.Information($"   Verfügbares Trading-Budget: {_availableTradingBudget:F2} EUR");
                    Log.Information($"   Geschützter Profit: {_protectedProfit:F2} EUR (NICHT verfügbar für Trading)");

                    return true;
                }
                else
                {
                    Log.Warning($"❌ Budget nicht ausreichend für {symbol}:");
                    Log.Warning($"   Benötigt: {purchaseAmount:F2} EUR");
                    Log.Warning($"   Verfügbares Trading-Budget: {_availableTradingBudget:F2} EUR");
                    Log.Warning($"   Geschützter Profit: {_protectedProfit:F2} EUR (geschützt vor Reinvestierung)");

                    return false;
                }
            }
        }

        /// <summary>
        /// Gibt Budget nach Verkauf frei - Gewinn wird geschützt
        /// </summary>
        public new void ReleaseBudgetFromSale(decimal originalInvestment, decimal saleProceeds, string symbol)
        {
            lock (_lock)
            {
                // 1. Ursprüngliches Investment zurück zum Trading-Budget
                _availableTradingBudget += originalInvestment;
                _totalInvested -= originalInvestment;

                // 2. Gewinn berechnen
                var profit = saleProceeds - originalInvestment;
                _totalRealizedProfit += profit;

                // 3. ✅ PROFIT-SCHUTZ: Bestimme wie viel Gewinn geschützt wird
                var protectedAmount = CalculateProtectedAmount(profit);
                var reinvestableAmount = profit - protectedAmount;

                _protectedProfit += protectedAmount;

                // 4. ✅ NUR der nicht-geschützte Teil kann reinvestiert werden
                if (reinvestableAmount > 0)
                {
                    // Prüfe ob Trading-Budget das Maximum überschreiten würde
                    var potentialNewBudget = _availableTradingBudget + reinvestableAmount;
                    if (potentialNewBudget > _maxTradingBudget)
                    {
                        var excessAmount = potentialNewBudget - _maxTradingBudget;
                        _protectedProfit += excessAmount; // Überschuss auch schützen
                        reinvestableAmount -= excessAmount;

                        Log.Warning($"⚠️ Trading-Budget-Maximum erreicht - zusätzliche {excessAmount:F2} EUR werden geschützt");
                    }

                    _availableTradingBudget += reinvestableAmount;
                }

                // 5. Logging
                Log.Information($"=== 💰 VERKAUF MIT PROFIT-SCHUTZ ===");
                Log.Information($"Symbol: {symbol}");
                Log.Information($"Ursprüngliches Investment: {originalInvestment:F2} EUR (zurück zu Trading-Budget)");
                Log.Information($"Verkaufserlös: {saleProceeds:F2} EUR");
                Log.Information($"Gesamtgewinn: {profit:F2} EUR");
                Log.Information($"Geschützter Gewinn: {protectedAmount:F2} EUR (NICHT reinvestierbar)");
                Log.Information($"Reinvestierbarer Gewinn: {reinvestableAmount:F2} EUR");
                Log.Information($"Verfügbares Trading-Budget: {_availableTradingBudget:F2} EUR");
                Log.Information($"Gesamter geschützter Profit: {_protectedProfit:F2} EUR");

                // Historie aktualisieren
                _profitHistory.Add((DateTime.UtcNow, profit, symbol, "SALE"));
                if (protectedAmount > 0)
                {
                    _profitHistory.Add((DateTime.UtcNow, protectedAmount, symbol, "PROTECTED"));
                }
            }
        }

        /// <summary>
        /// Berechnet wie viel Gewinn geschützt werden soll
        /// </summary>
        private decimal CalculateProtectedAmount(decimal profit)
        {
            if (profit <= 0) return 0;

            return _protectionMode switch
            {
                ProfitProtectionMode.Full => profit, // ✅ 100% des Gewinns schützen

                ProfitProtectionMode.Percentage => profit * (_protectionPercentage / 100m),

                ProfitProtectionMode.Threshold => profit > _protectionThreshold
                    ? profit - _protectionThreshold
                    : 0,

                _ => profit
            };
        }

        /// <summary>
        /// Strikte Budget-Prüfung - nur ursprüngliches Budget verfügbar
        /// </summary>
        public new bool CanAffordPurchaseStrict(decimal requiredAmount, string symbol)
        {
            lock (_lock)
            {
                var canAfford = _availableTradingBudget >= requiredAmount;

                if (!canAfford)
                {
                    Log.Information($"🛡️ Kauf abgelehnt für {symbol} (Profit-Schutz aktiv):");
                    Log.Information($"   Benötigt: {requiredAmount:F2} EUR");
                    Log.Information($"   Verfügbares Trading-Budget: {_availableTradingBudget:F2} EUR");
                    Log.Information($"   Geschützter Profit: {_protectedProfit:F2} EUR (nicht verfügbar)");
                    Log.Information($"   Initial Budget war: {_initialTradingBudget:F2} EUR");
                }

                return canAfford;
            }
        }

        /// <summary>
        /// Erweiterte Budget-Status mit Profit-Schutz-Details
        /// </summary>
        public (decimal AvailableTradingBudget, decimal TotalInvested, decimal TotalRealizedProfit,
                decimal ProtectedProfit, decimal InitialBudget, decimal MaxTradingBudget, decimal OverallPL) GetProtectedBudgetStatus()
        {
            lock (_lock)
            {
                return (_availableTradingBudget, _totalInvested, _totalRealizedProfit,
                        _protectedProfit, _initialTradingBudget, _maxTradingBudget, _overallPL);
            }
        }

        /// <summary>
        /// Konfiguriert den Profit-Schutz-Modus
        /// </summary>
        public void ConfigureProfitProtection(ProfitProtectionMode mode, decimal percentage = 80m, decimal threshold = 50m)
        {
            lock (_lock)
            {
                _protectionMode = mode;
                _protectionPercentage = Math.Max(0, Math.Min(100, percentage));
                _protectionThreshold = Math.Max(0, threshold);

                Log.Information($"🛡️ Profit-Schutz konfiguriert:");
                Log.Information($"   Modus: {_protectionMode}");

                if (_protectionMode == ProfitProtectionMode.Percentage)
                {
                    Log.Information($"   Schutz-Prozentsatz: {_protectionPercentage}%");
                }
                else if (_protectionMode == ProfitProtectionMode.Threshold)
                {
                    Log.Information($"   Schutz-Schwellenwert: {_protectionThreshold:F2} EUR");
                }
            }
        }

        /// <summary>
        /// Detaillierte Profit-Schutz-Statistiken
        /// </summary>
        public void LogProtectedProfitStatus()
        {
            lock (_lock)
            {
                var status = GetProtectedBudgetStatus();

                Log.Information("=== 🛡️ PROFIT-SCHUTZ STATUS ===");
                Log.Information($"💰 Verfügbares Trading-Budget: {status.AvailableTradingBudget:F2} EUR");
                Log.Information($"📊 Aktuell investiert: {status.TotalInvested:F2} EUR");
                Log.Information($"💎 Gesamter realisierter Profit: {status.TotalRealizedProfit:F2} EUR");
                Log.Information($"🛡️ Geschützter Profit: {status.ProtectedProfit:F2} EUR");
                Log.Information($"🔄 Reinvestierbarer Betrag: {status.AvailableTradingBudget + status.TotalInvested:F2} EUR");
                Log.Information($"🎯 Initial Budget: {status.InitialBudget:F2} EUR");
                Log.Information($"🚧 Max Trading-Budget: {status.MaxTradingBudget:F2} EUR");

                var protectionRatio = status.TotalRealizedProfit > 0
                    ? status.ProtectedProfit / status.TotalRealizedProfit * 100
                    : 0;

                Log.Information($"📈 Profit-Schutz-Rate: {protectionRatio:F1}%");

                // Profit-Historie der letzten 5 Trades
                var recentTrades = _profitHistory
                    .Where(h => h.Type == "SALE")
                    .OrderByDescending(h => h.Timestamp)
                    .Take(5)
                    .ToList();

                if (recentTrades.Any())
                {
                    Log.Information("📊 Letzte 5 Trades:");
                    foreach (var trade in recentTrades)
                    {
                        var icon = trade.Profit >= 0 ? "💚" : "❤️";
                        Log.Information($"   {icon} {trade.Symbol}: {trade.Profit:F2} EUR ({trade.Timestamp:HH:mm:ss})");
                    }
                }
            }
        }

        /// <summary>
        /// Prüft ob Emergency-Modus aktiviert werden sollte
        /// </summary>
        public bool ShouldActivateEmergencyMode()
        {
            lock (_lock)
            {
                // Emergency wenn 90% des ursprünglichen Budgets verloren
                var totalValue = _availableTradingBudget + _totalInvested;
                var lossPercentage = (_initialTradingBudget - totalValue) / _initialTradingBudget * 100;

                return lossPercentage >= 90m;
            }
        }

        /// <summary>
        /// Notfall-Stopp: Alle Positionen verkaufen und Trading stoppen
        /// </summary>
        public void ActivateEmergencyMode()
        {
            lock (_lock)
            {
                Log.Error("🚨 NOTFALL-MODUS AKTIVIERT 🚨");
                Log.Error("Trading wird gestoppt - alle Positionen sollten verkauft werden!");

                var status = GetProtectedBudgetStatus();
                var remainingValue = status.AvailableTradingBudget + status.TotalInvested;
                var totalLoss = status.InitialBudget - remainingValue;
                var lossPercentage = totalLoss / status.InitialBudget * 100;

                Log.Error($"💸 Gesamtverlust: {totalLoss:F2} EUR ({lossPercentage:F1}%)");
                Log.Error($"🛡️ Geschützter Profit: {status.ProtectedProfit:F2} EUR (SICHER)");
            }
        }
    }

    /// <summary>
    /// Profit-Schutz-Konfiguration für UI
    /// </summary>
    public class ProfitProtectionConfig
    {
        public ProfitProtectionMode Mode { get; set; } = ProfitProtectionMode.Full;
        public decimal ProtectionPercentage { get; set; } = 80m;
        public decimal ProtectionThreshold { get; set; } = 50m;
        public decimal MaxTradingBudgetMultiplier { get; set; } = 1.0m; // 1.0 = nur ursprüngliches Budget
        public bool EmergencyStopEnabled { get; set; } = true;
        public decimal EmergencyStopLossPercentage { get; set; } = 90m;

        public string GetDescription()
        {
            return Mode switch
            {
                ProfitProtectionMode.Full => "100% des Gewinns wird geschützt (nicht reinvestiert)",
                ProfitProtectionMode.Percentage => $"{ProtectionPercentage}% des Gewinns wird geschützt",
                ProfitProtectionMode.Threshold => $"Gewinn über {ProtectionThreshold:F2} EUR wird geschützt",
                _ => "Unbekannter Modus"
            };
        }
    }
}