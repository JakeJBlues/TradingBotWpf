using TradingBotCore.Entities;

namespace TradingBotCore.Interfaces
{
    public interface ITradingPosition
    {
        int AverageDownCount { get; set; }
        bool AverageDownEnabled { get; set; }
        List<AverageDownEntry> AverageDownHistory { get; set; }
        decimal CurrentMarketPrice { get; set; }
        decimal High { get; set; }
        DateTime LastAverageDownTime { get; set; }
        decimal NextAverageDownTrigger { get; set; }
        string OrderId { get; set; }
        decimal OriginalPurchasePrice { get; set; }
        double OriginalVolume { get; set; }
        DateTime Processed { get; set; }
        decimal PurchasePrice { get; set; }
        string Symbol { get; set; }
        decimal TotalInvestedAmount { get; set; }
        double Volume { get; set; }

        (decimal UnrealizedPL, decimal UnrealizedPLPercent) CalculateUnrealizedPL(decimal currentPrice);
        bool CanSell(decimal currentPrice, double greenRatio = 0);
        void DisableAverageDown(string reason = "");
        decimal ExecuteAverageDown(decimal currentPrice, decimal additionalInvestment);
        string GetDetailedInfo(decimal currentPrice);
        void InitializePosition(decimal price, double volume, decimal investedAmount);
        void notifiyUI();
        bool ShouldTriggerAverageDown(decimal currentPrice);
    }
}