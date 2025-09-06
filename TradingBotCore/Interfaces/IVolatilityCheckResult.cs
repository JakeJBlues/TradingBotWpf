// Ergebnis der Volatilitätsprüfung
public interface IVolatilityCheckResult
{
    decimal ActualVolatilityPercent { get; set; }
    decimal AveragePrice { get; set; }
    int CandlesAboveAverage { get; set; }
    int CandlesAtAverage { get; set; }
    int CandlesBelowAverage { get; set; }
    decimal DistributionRatio { get; set; }
    bool MeetsDistributionRequirement { get; set; }
    bool MeetsVolatilityRequirement { get; set; }
    bool OverallResult { get; }
}