using TradingBotCore.Helper;

namespace TradingBotCore.Interfaces
{
    public interface IProtectedProfitTracker
    {
        void ActivateEmergencyMode();
        bool CanAffordPurchaseStrict(decimal requiredAmount, string symbol);
        void ConfigureProfitProtection(ProtectedProfitTracker.ProfitProtectionMode mode, decimal percentage = 80, decimal threshold = 50);
        (decimal AvailableTradingBudget, decimal TotalInvested, decimal TotalRealizedProfit, decimal ProtectedProfit, decimal InitialBudget, decimal MaxTradingBudget, decimal OverallPL) GetProtectedBudgetStatus();
        void LogProtectedProfitStatus();
        void ReleaseBudgetFromSale(decimal originalInvestment, decimal saleProceeds, string symbol);
        bool ReserveBudgetForPurchase(decimal purchaseAmount, string symbol);
        void SetInitialBalance(decimal balance);
        void SetOverAllPL(decimal profit);
        bool ShouldActivateEmergencyMode();
    }
}