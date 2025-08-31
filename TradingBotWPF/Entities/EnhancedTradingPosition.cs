using System;
using System.ComponentModel;
using System.Windows.Media;
using TradingBotCore.Entities;
/// Erweiterte TradingPosition mit INotifyPropertyChanged für UI-Binding
/// </summary>
public class EnhancedTradingPosition : TradingPosition, INotifyPropertyChanged
{
    private decimal _currentMarketPrice;
    private DateTime _lastPriceUpdate;
    private bool _isPriceRising;


    public decimal EstimatedPrice { get; set; }  // Der geschätzte Preis vor Order
    public decimal ActualPrice { get; set; }     // Der tatsächliche Preis nach Order
    public decimal PriceSlippage { get; set; }   // Abweichung in %
    public bool HasActualPrice { get; set; }     // Ob tatsächlicher Preis verfügbar ist

    /// <summary>
    /// Initialisiert Position mit geschätztem Preis, wird später mit echtem Preis aktualisiert
    /// </summary>
    public void InitializeWithEstimatedPrice(decimal estimatedPrice, double volume, decimal investedAmount)
    {
        EstimatedPrice = estimatedPrice;
        ActualPrice = EstimatedPrice; // Wird später gesetzt
        HasActualPrice = false;

        // Verwende erstmal geschätzten Preis
        InitializePosition(estimatedPrice, volume, investedAmount);
    }

    /// <summary>
    /// Aktualisiert Position mit dem tatsächlichen Kaufpreis
    /// </summary>
    public void UpdateWithActualPrice(decimal actualPrice)
    {
        if (actualPrice <= 0) return;

        ActualPrice = actualPrice;
        CurrentMarketPrice = actualPrice; // Setze aktuellen Marktpreis auf tatsächlichen Kaufpreis
        HasActualPrice = true;

        // Berechne Slippage
        if (EstimatedPrice > 0)
        {
            PriceSlippage = ((actualPrice - EstimatedPrice) / EstimatedPrice) * 100;
        }

        // ✅ Aktualisiere alle Preis-abhängigen Werte mit ECHTEM Preis
        PurchasePrice = (PurchasePrice + CurrentMarketPrice) / 2;

        // Average-Down Trigger basierend auf ECHTEM Preis
        NextAverageDownTrigger = OriginalPurchasePrice * (1 - (AverageDownCount + 1) * 0.02m);
    }

    // Live-Preis-Properties
    public override void notifiyUI()
    {
        OnPropertyChanged(nameof(CurrentMarketPrice));
        OnPropertyChanged(nameof(CurrentMarketPriceFormatted));
        OnPropertyChanged(nameof(UnrealizedPL));
        OnPropertyChanged(nameof(UnrealizedPLPercent));
        OnPropertyChanged(nameof(UnrealizedPLFormatted));
        OnPropertyChanged(nameof(ProfitLossColor));
        OnPropertyChanged(nameof(CanSellNow));
        OnPropertyChanged(nameof(SellIndicator));
        OnPropertyChanged(nameof(PriceChangeDirection));
        OnPropertyChanged(nameof(LastPriceUpdateFormatted));
        base.notifiyUI();
    }

    // Berechnete Properties für UI
    public string CurrentMarketPriceFormatted => $"{CurrentMarketPrice:F6} EUR";
    public string LastPriceUpdateFormatted => $"Update: {_lastPriceUpdate:HH:mm:ss}";
    public string PriceChangeDirection => _isPriceRising ? "📈" : "📉";

    // Erweiterte P/L-Berechnung mit Live-Preis
    public new (decimal UnrealizedPL, decimal UnrealizedPLPercent) CalculateUnrealizedPL(decimal currentPrice)
    {
        CurrentMarketPrice = currentPrice;
        ActualPrice = currentPrice; // Setze tatsächlichen Preis
        return base.CalculateUnrealizedPL(currentPrice);
    }

    public decimal UnrealizedPL
    {
        get
        {
            if (CurrentMarketPrice > 0)
            {
                var (pl, _) = base.CalculateUnrealizedPL(CurrentMarketPrice);
                return pl;
            }
            return 0;
        }
    }

    public decimal UnrealizedPLPercent
    {
        get
        {
            if (CurrentMarketPrice > 0)
            {
                var (_, plPercent) = base.CalculateUnrealizedPL(CurrentMarketPrice);
                return plPercent;
            }
            return 0;
        }
    }

    public string UnrealizedPLFormatted => $"{UnrealizedPL:F2} EUR ({UnrealizedPLPercent:F2}%)";
    public Brush ProfitLossColor => UnrealizedPL >= 0 ? Brushes.Green : Brushes.Red;

    // Erweiterte Verkaufs-Logik
    public bool CanSellNow => CurrentMarketPrice > High || base.CanSell(CurrentMarketPrice);
    public string SellIndicator => CanSellNow ? "🎯 VERKAUFEN" : "⏳ Halten";
    public Brush SellIndicatorColor => CanSellNow ? Brushes.Orange : Brushes.Gray;

    // Preis-Trend-Analyse
    public string GetPriceTrendAnalysis()
    {
        if (CurrentMarketPrice <= 0) return "Keine Daten";

        var changeFromPurchase = ((CurrentMarketPrice - PurchasePrice) / PurchasePrice) * 100;
        var changeFromOriginal = ((CurrentMarketPrice - OriginalPurchasePrice) / OriginalPurchasePrice) * 100;
        var distanceToTarget = ((High - CurrentMarketPrice) / CurrentMarketPrice) * 100;

        return $"Vs. Ø-Preis: {changeFromPurchase:F2}% | Vs. Original: {changeFromOriginal:F2}% | Bis Ziel: {distanceToTarget:F2}%";
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>