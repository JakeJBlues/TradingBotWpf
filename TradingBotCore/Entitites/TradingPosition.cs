using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Serilog;
using TradingBotCore;

namespace TradingBotCore.Entities
{
    // Erweiterte Thread-sichere Position-Verwaltung mit Average-
    public class TradingPosition
    {
        public string Symbol { get; set; }
        public decimal High { get; set; }
        public DateTime Processed { get; set; }
        public double Volume { get; set; }
        public decimal PurchasePrice { get; set; }
        public string OrderId { get; set; }

        // Neue Properties für Average-Down
        public decimal OriginalPurchasePrice { get; set; }
        public double OriginalVolume { get; set; }
        public decimal TotalInvestedAmount { get; set; }
        public int AverageDownCount { get; set; }
        public List<AverageDownEntry> AverageDownHistory { get; set; }
        public decimal NextAverageDownTrigger { get; set; }
        public bool AverageDownEnabled { get; set; } = Login.AverageDownEnabled;
        public DateTime LastAverageDownTime { get; set; }

        private decimal _currentMarketPrice = 0;
        private bool _isPriceRising = false;
        private DateTime _lastPriceUpdate = DateTime.MinValue;

        public TradingPosition()
        {
            AverageDownHistory = new List<AverageDownEntry>();
            AverageDownEnabled = Login.AverageDownEnabled;
            AverageDownCount = 0;
            LastAverageDownTime = DateTime.MinValue;
        }

        // Initialisiert Position beim ersten Kauf
        public void InitializePosition(decimal price, double volume, decimal investedAmount)
        {
            Volume = volume * 0.999;
            OriginalVolume = volume * 0.999;
            PurchasePrice = (investedAmount / (decimal)volume);
            OriginalPurchasePrice = (investedAmount / (decimal)volume);
            TotalInvestedAmount = investedAmount;

            // Trigger für ersten Average-Down bei 2% Verlust
            NextAverageDownTrigger = OriginalPurchasePrice * (1 - (AverageDownCount + 1) * 0.02m);

            Log.Information($"Position initialisiert für {Symbol}: {price:F6} EUR, Volume: {volume:F4}, Trigger: {NextAverageDownTrigger:F6}");
        }

        public decimal CurrentMarketPrice
        {
            get => _currentMarketPrice;
            set
            {
                var oldPrice = _currentMarketPrice;
                _currentMarketPrice = value;
                _isPriceRising = value > oldPrice && oldPrice > 0;
                _lastPriceUpdate = DateTime.UtcNow;
                notifiyUI();
            }
        }

        public virtual void notifiyUI()
        {
        }

        // Prüft ob Average-Down ausgelöst werden soll
        public bool ShouldTriggerAverageDown(decimal currentPrice)
        {
            if (!AverageDownEnabled || AverageDownCount >= 3) // Maximal 3 Average-Downs
            {
                Log.Information($"Average-Down für {Symbol} deaktiviert oder Limit erreicht.");
                return false;
            }

            // Mindestabstand zwischen Average-Downs (5 Minuten)
            if (DateTime.UtcNow - LastAverageDownTime < TimeSpan.FromMinutes(5))
            {
                Log.Information($"Average-Down für {Symbol} übersprungen: Wartezeit von 5 Minuten nicht erreicht.");
                return false;
            }

            // Prüfe ob Preis unter Trigger gefallen ist
            bool shouldTrigger = currentPrice <= NextAverageDownTrigger;

            Log.Information($"Average-Down Trigger für {Symbol}: Aktuell {currentPrice:F6} <= Trigger {NextAverageDownTrigger:F6} {shouldTrigger}");

            return shouldTrigger;
        }

        // Führt Average-Down durch
        public decimal ExecuteAverageDown(decimal currentPrice, decimal additionalInvestment)
        {
            var additionalVolume = additionalInvestment / currentPrice;

            // Erstelle History-Eintrag
            var averageDownEntry = new AverageDownEntry
            {
                Timestamp = DateTime.UtcNow,
                Price = currentPrice,
                Volume = (double)additionalVolume,
                InvestedAmount = additionalInvestment,
                PreviousAveragePrice = PurchasePrice
            };

            AverageDownHistory.Add(averageDownEntry);

            // Aktualisiere Position
            var newTotalVolume = Volume + (double)additionalVolume;
            var newTotalInvestment = TotalInvestedAmount + additionalInvestment;
            var newAveragePrice = newTotalInvestment / (decimal)newTotalVolume;

            // Position aktualisieren
            Volume = newTotalVolume;
            TotalInvestedAmount = newTotalInvestment;
            PurchasePrice = newAveragePrice; // Neuer Durchschnittspreis
            AverageDownCount++;
            LastAverageDownTime = DateTime.UtcNow;

            // Nächsten Trigger berechnen (weitere 1% vom neuen Durchschnitt)
            NextAverageDownTrigger = newAveragePrice * 0.98m;

            // WICHTIG: Verkaufsziel (High) bleibt unverändert - das ursprüngliche Ziel!
            // High wird NICHT verändert, damit das ursprüngliche Verkaufsziel bestehen bleibt

            Log.Information($"=== AVERAGE-DOWN AUSGEFÜHRT ===");
            Log.Information($"Symbol: {Symbol}");
            Log.Information($"Zusätzlicher Kauf: {additionalInvestment:F2} EUR bei {currentPrice:F6}");
            Log.Information($"Neuer Durchschnittspreis: {newAveragePrice:F6} EUR (vorher: {averageDownEntry.PreviousAveragePrice:F6})");
            Log.Information($"Gesamtvolume: {newTotalVolume:F4} (vorher: {newTotalVolume - (double)additionalVolume:F4})");
            Log.Information($"Gesamtinvestment: {newTotalInvestment:F2} EUR");
            Log.Information($"Average-Down #{AverageDownCount}/3");
            Log.Information($"Nächster Trigger: {NextAverageDownTrigger:F6} EUR");
            Log.Information($"Verkaufsziel bleibt: {High:F6} EUR (UNVERÄNDERT)");

            return newAveragePrice;
        }

        // Berechnet aktuellen Profit/Loss
        public (decimal UnrealizedPL, decimal UnrealizedPLPercent) CalculateUnrealizedPL(decimal currentPrice)
        {
            var currentValue = currentPrice * (decimal)Volume;
            var unrealizedPL = currentValue - TotalInvestedAmount;
            var unrealizedPLPercent = unrealizedPL / TotalInvestedAmount * 100;

            return (unrealizedPL, unrealizedPLPercent);
        }

        // Gibt detaillierte Position-Informationen zurück
        public string GetDetailedInfo(decimal currentPrice)
        {
            var (unrealizedPL, unrealizedPLPercent) = CalculateUnrealizedPL(currentPrice);

            var info = $"""
                === POSITION DETAILS: {Symbol} ===
                Aktueller Preis: {currentPrice:F6} EUR
                Durchschnittspreis: {PurchasePrice:F6} EUR (Original: {OriginalPurchasePrice:F6})
                Volume: {Volume:F4} (Original: {OriginalVolume:F4})
                Gesamtinvestment: {TotalInvestedAmount:F2} EUR
                Unrealisierter P/L: {unrealizedPL:F2} EUR ({unrealizedPLPercent:F2}%)
                Average-Downs: {AverageDownCount}/3
                Nächster Trigger: {NextAverageDownTrigger:F6} EUR
                Verkaufsziel: {High:F6} EUR (URSPRÜNGLICH - bleibt unverändert)
                """;

            if (AverageDownHistory.Any())
            {
                info += "\n--- Average-Down Historie ---\n";
                for (int i = 0; i < AverageDownHistory.Count; i++)
                {
                    var entry = AverageDownHistory[i];
                    info += $"#{i + 1}: {entry.Timestamp:HH:mm:ss} - {entry.Price:F6} EUR, +{entry.Volume:F4} Vol, +{entry.InvestedAmount:F2} EUR\n";
                }
            }

            return info;
        }

        // Prüft ob Position verkauft werden kann (Profit-Bedingung)
        public bool CanSell(decimal currentPrice, double greenRatio = 0)
        {
            // Verkaufen nur wenn Gewinn gemacht wird
            if (greenRatio == 0)
                return currentPrice > High; // Mindestens 0.5% Gewinn
            else
                return currentPrice > (High / 1.005m) * (decimal)(1 + greenRatio); // Mindestens 0.5% Gewinn
        }

        // Deaktiviert weitere Average-Downs
        public void DisableAverageDown(string reason = "")
        {
            AverageDownEnabled = false;
            Log.Information($"Average-Down für {Symbol} deaktiviert. Grund: {reason}");
        }
    }
}