using TradingBotCore.Entities;

namespace TradingBotCore.Interfaces
{
    public interface IProfitTracker
    {
        decimal CalculateExpectedProfit(List<TradingPosition> positions, Func<string, Task<decimal>> getCurrentPrice);
        bool CanAffordPurchase(decimal requiredAmount, decimal currentEurBalance);
        bool CanAffordPurchaseStrict(decimal requiredAmount, string symbol);
        (decimal AvailableBudget, decimal TotalInvested, decimal TotalProfit, decimal InitialBalance) GetBudgetStatus();
        (decimal TotalRealized, decimal CurrentPortfolio, decimal TotalProfit, decimal ProtectedProfit, decimal HighWaterMark) GetProfitSummary(decimal currentBalance);
        List<(DateTime Timestamp, decimal Profit, string Symbol)> GetRecentTrades(int count = 10);
        void LogProfitStatus(decimal currentBalance, List<TradingPosition> positions = null, Func<string, Task<decimal>> getCurrentPrice = null);
        void RecordProfit(decimal totalSaleValue, string symbol);
        void ReleaseBudgetFromSale(decimal originalInvestment, decimal saleProceeds, string symbol);
        bool ReserveBudgetForPurchase(decimal purchaseAmount, string symbol);
        void SetInitialBalance(decimal balance);
    }
}