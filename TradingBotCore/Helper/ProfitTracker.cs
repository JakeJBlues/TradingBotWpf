using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using TradingBotCore.Entities;
using TradingBotCore.Interfaces;

namespace TradingBotCore.Helper
{
    // Erweiterte Profit-Tracking-Klasse mit strikter Trading-Budget-Kontrolle
    public class ProfitTracker : IProfitTracker
    {
        private decimal _initialBalance = 0;
        private decimal _totalRealizedProfit = 0;
        private decimal _maxTradingBalance = 0;
        private decimal _currentTradingBudget = 0;
        private decimal _totalInvested = 0;
        private decimal _highWaterMark = 0;
        private readonly List<(DateTime Timestamp, decimal Profit, string Symbol)> _profitHistory = new();
        private readonly List<(DateTime Timestamp, decimal Amount, string Symbol, string Action)> _budgetHistory = new();
        private readonly object _lock = new object();

        public void SetInitialBalance(decimal balance)
        {
            lock (_lock)
            {
                if (_initialBalance == 0)
                {
                    _initialBalance = balance;
                    _maxTradingBalance = balance;
                    _currentTradingBudget = balance;
                    _highWaterMark = 0;

                    Log.Information($"=== TRADING-BUDGET INITIALISIERT ===");
                    Log.Information($"Initial-Balance: {_initialBalance:F2} EUR");
                    Log.Information($"Verfügbares Trading-Budget: {_currentTradingBudget:F2} EUR");

                    _budgetHistory.Add((DateTime.UtcNow, balance, "INITIAL", "BUDGET_SET"));
                }
            }
        }

        // Budget für Kauf reservieren
        public bool ReserveBudgetForPurchase(decimal purchaseAmount, string symbol)
        {
            lock (_lock)
            {
                if (_currentTradingBudget >= purchaseAmount)
                {
                    _currentTradingBudget -= purchaseAmount;
                    _totalInvested += purchaseAmount;

                    _budgetHistory.Add((DateTime.UtcNow, purchaseAmount, symbol, "PURCHASE"));

                    Log.Information($"Budget reserviert für {symbol}: {purchaseAmount:F2} EUR | Verbleibend: {_currentTradingBudget:F2} EUR");
                    return true;
                }
                else
                {
                    Log.Warning($"Budget nicht ausreichend für {symbol}: Benötigt {purchaseAmount:F2} EUR, verfügbar {_currentTradingBudget:F2} EUR");
                    return false;
                }
            }
        }

        // Budget nach Verkauf freigeben
        public void ReleaseBudgetFromSale(decimal originalInvestment, decimal saleProceeds, string symbol)
        {
            lock (_lock)
            {
                _currentTradingBudget += originalInvestment;
                _totalInvested -= originalInvestment;

                var profit = saleProceeds - originalInvestment;
                _totalRealizedProfit += profit;

                _budgetHistory.Add((DateTime.UtcNow, originalInvestment, symbol, "SALE_BUDGET_RELEASE"));
                if (profit != 0)
                {
                    _budgetHistory.Add((DateTime.UtcNow, profit, symbol, profit > 0 ? "PROFIT" : "LOSS"));
                }

                Log.Information($"Budget freigegeben für {symbol}: {originalInvestment:F2} EUR | Profit: {profit:F2} EUR | Verfügbar: {_currentTradingBudget:F2} EUR");
            }
        }

        // Strikte Budget-Prüfung
        public bool CanAffordPurchaseStrict(decimal requiredAmount, string symbol)
        {
            lock (_lock)
            {
                var canAfford = _currentTradingBudget >= requiredAmount;

                if (!canAfford)
                {
                    Log.Information($"Kauf abgelehnt für {symbol}: Benötigt {requiredAmount:F2} EUR, verfügbar {_currentTradingBudget:F2} EUR");
                }

                return canAfford;
            }
        }

        // Budget-Status abrufen
        public (decimal AvailableBudget, decimal TotalInvested, decimal TotalProfit, decimal InitialBalance) GetBudgetStatus()
        {
            lock (_lock)
            {
                return (_currentTradingBudget, _totalInvested, _totalRealizedProfit, _initialBalance);
            }
        }

        // Kompatibilität mit alter Methode
        public bool CanAffordPurchase(decimal requiredAmount, decimal currentEurBalance)
        {
            return CanAffordPurchaseStrict(requiredAmount, "LEGACY_CHECK");
        }

        public void RecordProfit(decimal totalSaleValue, string symbol)
        {
            // Kompatibilität - wird durch ReleaseBudgetFromSale ersetzt
            Log.Debug($"RecordProfit aufgerufen für {symbol}: {totalSaleValue:F2} EUR");
        }

        public (decimal TotalRealized, decimal CurrentPortfolio, decimal TotalProfit, decimal ProtectedProfit, decimal HighWaterMark) GetProfitSummary(decimal currentBalance)
        {
            lock (_lock)
            {
                var currentPortfolioValue = _currentTradingBudget + _totalInvested + _totalRealizedProfit;
                var totalProfit = currentPortfolioValue - _initialBalance;

                if (totalProfit > _highWaterMark)
                {
                    _highWaterMark = totalProfit;
                }

                return (_totalRealizedProfit, currentPortfolioValue, totalProfit, _highWaterMark, _highWaterMark);
            }
        }

        public decimal CalculateExpectedProfit(List<TradingPosition> positions, Func<string, Task<decimal>> getCurrentPrice)
        {
            decimal totalExpectedProfitEUR = 0;

            foreach (var position in positions)
            {
                try
                {
                    var currentPrice = getCurrentPrice(position.Symbol).Result;
                    if (currentPrice > 0)
                    {
                        var expectedSellValueEUR = position.High * (decimal)position.Volume;
                        var originalInvestmentEUR = position.TotalInvestedAmount; // Geändert für Average-Down
                        var expectedProfitEUR = expectedSellValueEUR - originalInvestmentEUR;
                        totalExpectedProfitEUR += expectedProfitEUR;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"Fehler beim Abrufen des Preises für {position.Symbol}: {ex.Message}");
                }
            }

            return totalExpectedProfitEUR;
        }

        public void LogProfitStatus(decimal currentBalance, List<TradingPosition> positions = null, Func<string, Task<decimal>> getCurrentPrice = null)
        {
            decimal expectedProfit = 0;
            if (positions != null && getCurrentPrice != null && positions.Any())
            {
                expectedProfit = CalculateExpectedProfit(positions, getCurrentPrice);
            }

            var budgetStatus = GetBudgetStatus();

            Log.Information("=== PROFIT & BUDGET STATUS ===");
            Log.Information($"💰 Budget verfügbar: {budgetStatus.AvailableBudget:F2} EUR");
            Log.Information($"📊 Aktuell investiert: {budgetStatus.TotalInvested:F2} EUR");
            Log.Information($"💎 Realisierter Profit: {budgetStatus.TotalProfit:F2} EUR");
            Log.Information($"🎯 Budget-Auslastung: {budgetStatus.TotalInvested / budgetStatus.InitialBalance * 100:F1}%");

            if (expectedProfit > 0)
            {
                Log.Information($"📈 Erwarteter Profit: {expectedProfit:F2} EUR");
            }
        }

        public List<(DateTime Timestamp, decimal Profit, string Symbol)> GetRecentTrades(int count = 10)
        {
            lock (_lock)
            {
                return _profitHistory.OrderByDescending(t => t.Timestamp).Take(count).ToList();
            }
        }
    }
}