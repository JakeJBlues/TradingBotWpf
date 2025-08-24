// Ergebnis der Volatilitätsprüfung
using OKX.Net.Objects.Market;
using System;
using System.Linq;

public class VolatilityCheckResult
{
    public bool MeetsVolatilityRequirement { get; set; }
    public bool MeetsDistributionRequirement { get; set; }
    public bool OverallResult => MeetsVolatilityRequirement && MeetsDistributionRequirement;

    // Zusätzliche Informationen
    public decimal ActualVolatilityPercent { get; set; }
    public decimal AveragePrice { get; set; }
    public int CandlesAboveAverage { get; set; }
    public int CandlesBelowAverage { get; set; }
    public int CandlesAtAverage { get; set; }
    public decimal DistributionRatio { get; set; }
}

public class VolatilityAnalyzer
{
    /// <summary>
    /// Prüft ob die Kerzen die Volatilitätsbedingungen erfüllen
    /// </summary>
    /// <param name="klines">Array von OKX Klines</param>
    /// <param name="minVolatilityPercent">Mindest-Volatilität in Prozent (Standard: 1%)</param>
    /// <param name="maxDistributionImbalance">Maximale Abweichung von 50:50 Verteilung (Standard: 0.3 = 30%)</param>
    /// <returns>VolatilityCheckResult mit detaillierten Informationen</returns>
    public VolatilityCheckResult CheckVolatilityConditions(
        OKXKline[] klines,
        decimal minVolatilityPercent = 1.0m,
        decimal maxDistributionImbalance = 0.3m)
    {
        if (klines == null || klines.Length < 2)
            throw new ArgumentException("Mindestens 2 Klines erforderlich");

        var result = new VolatilityCheckResult();

        // 1. Volatilitätsprüfung: Min/Max Spanne über alle Kerzen
        var allHighs = klines.Select(k => k.HighPrice);
        var allLows = klines.Select(k => k.LowPrice);
        var maxHigh = allHighs.Max();
        var minLow = allLows.Min();
        var averagePrice = klines.Select(k => (k.HighPrice + k.LowPrice + k.ClosePrice) / 3).Average();

        result.AveragePrice = averagePrice;
        result.ActualVolatilityPercent = ((maxHigh - minLow) / averagePrice) * 100;
        result.MeetsVolatilityRequirement = result.ActualVolatilityPercent >= minVolatilityPercent;

        // 2. Verteilungsprüfung: Kerzen über/unter Durchschnitt
        var candlesAbove = 0;
        var candlesBelow = 0;
        var candlesAt = 0;

        foreach (var kline in klines.OrderByDescending(k => k.Time).Take(5))
        {
            // Verwende den Schlusskurs für die Verteilungsanalyse
            if (kline.ClosePrice >= kline.OpenPrice)
                candlesAbove++;
            else
                candlesBelow++;
        }

        var candlesRaise = candlesAbove > candlesBelow;
        result.MeetsDistributionRequirement = candlesRaise;
        return result;
    }

    /// <summary>
    /// Vereinfachte Methode die nur true/false zurückgibt
    /// </summary>
    public bool HasSufficientVolatility(OKXKline[] klines, decimal minVolatilityPercent = 1.0m, decimal maxDistributionImbalance = 0.3m)
    {
        var result = CheckVolatilityConditions(klines, minVolatilityPercent, maxDistributionImbalance);
        return result.MeetsVolatilityRequirement && result.MeetsDistributionRequirement;
    }
}
