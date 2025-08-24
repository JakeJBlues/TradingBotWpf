using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OKX.Net;
using OKX.Net.Enums;
using OKX.Net.Objects;
using OKX.Net.Objects.Public;
using CryptoExchange.Net.Authentication;
using Serilog;
using OKX.Net.Clients;
using TradingBotWPF.Interfaces;
using TradingBotWPF.Strategies;
using TradingBotWPF.Manager;
using TradingBotWPF.Entities;
using TradingBotWPF.Helper;

namespace TradingBot
{

    class Program
    {
        private static readonly TradingCooldownManager _cooldownManager = new(
            buyDelay: TimeSpan.FromMinutes(10),
            sellDelay: TimeSpan.FromSeconds(0),
            globalCooldown: TimeSpan.FromMicroseconds(100),
            sellLockoutDuration: TimeSpan.FromMinutes(5)
        );

        private static readonly PositionManager _positionManager = new();
        private static readonly ProtectedProfitTracker _profitTracker = new();
        private static readonly TradingBlacklistManager _blacklistManager = new();
        static Dictionary<string, OKXInstrument> InstrumentInfos = new Dictionary<string, OKXInstrument>();

        static async Task Main()
        {
            var stack = new AutoTrimStack<double>(5);
            Log.Logger = new LoggerConfiguration()
                 .MinimumLevel.Debug()
                 .WriteTo.Console(
                     outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                 .WriteTo.File("logs/tradingbot.log",
                     rollingInterval: RollingInterval.Day,
                     outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                 .CreateLogger();

            var client = new OKXRestClient(options =>
            {
                options.ApiCredentials = new ApiCredentials("c77c9afa-fed2-4eba-915c-f8d4eb23aba2", "63686C8CB72F797FB94CB63E5E1A6776", "oGlennyweg2311!x");
                options.Environment = OKXEnvironment.Europe;
            });

            var asset = await client.UnifiedApi.Account.GetAccountBalanceAsync();
            var symbolsResponse = await client.UnifiedApi.ExchangeData.GetSymbolsAsync(InstrumentType.Spot);
            if (!symbolsResponse.Success)
            {
                Log.Error($"Fehler beim Laden der Symbole: {symbolsResponse.Error}");
                return;
            }

            // Symbole über Blacklist filtern
            var allEurSymbols = symbolsResponse.Data
                .Where(s => s.Symbol.Contains("EUR"))
                .Select(s => s.Symbol)
                .Distinct()
                .ToList();

            var eurSymbols = _blacklistManager.FilterAllowedSymbols(allEurSymbols);

            Log.Information($"Symbol-Filterung: {allEurSymbols.Count} EUR-Symbole gefunden, {eurSymbols.Count} nach Blacklist-Filterung erlaubt");
            _blacklistManager.LogBlacklistStatus();

            InstrumentInfos = symbolsResponse.Data
                .Where(s => eurSymbols.Contains(s.Symbol))
                .ToDictionary(s => s.Symbol, s => s);

            // Initial-Verkauf aller bestehenden Assets (außer EUR)
            if (asset != null)
            {
                Log.Information("Führe Initial-Verkauf aller Assets durch...");

                foreach (var item in asset.Data.Details)
                {
                    var sym = eurSymbols.FirstOrDefault(s => s.StartsWith(item.Asset, StringComparison.OrdinalIgnoreCase));

                    if (sym != null && item.Asset != "EUR" && (item.AvailableBalance ?? 0m) > 0 &&
                        InstrumentInfos.ContainsKey($"{item.Asset}-EUR") &&
                        _blacklistManager.IsSymbolAllowed(sym))
                    {
                        var sellResponse = await client.UnifiedApi.Trading.PlaceOrderAsync(sym, OrderSide.Sell, OrderType.Market,
                            tradeMode: TradeMode.Cash, quantity: item.AvailableBalance ?? 0m);

                        if (sellResponse.Success)
                        {
                            Log.Information($"Initial-Verkauf erfolgreich: {item.Asset} ({sym})");
                        }
                        else
                        {
                            Log.Error($"Initial-Verkauf Fehler {item.Asset}: {sellResponse.Error}");
                        }
                    }
                    else if (item.Asset == "EUR")
                    {
                        Log.Information($"EUR-Balance gefunden: {(item.AvailableBalance ?? 0m):F2} EUR");
                    }
                }
            }

            // Trading-Budget nach Initial-Verkauf setzen
            await Task.Delay(3000);

            var finalAsset = await client.UnifiedApi.Account.GetAccountBalanceAsync();
            if (finalAsset?.Data?.Details != null)
            {
                var eurBalance = finalAsset.Data.Details.FirstOrDefault(d => d.Asset == "EUR")?.AvailableBalance ?? 0m;

                _profitTracker.SetInitialBalance(eurBalance);

                Log.Information($"=== 🚀 TRADING-BOT MIT AVERAGE-DOWN GESTARTET ===");
                Log.Information($"💰 Trading-Budget gesetzt: {eurBalance:F2} EUR");
                Log.Information($"🔒 Profit-Schutz aktiviert: Gewinn wird NICHT reinvestiert");
                Log.Information($"📊 Budget-Kontrolle aktiv: Käufe reduzieren verfügbares Budget");
                Log.Information($"🔄 Average-Down aktiviert: Max. 3 Nachkäufe bei 1% Kursrückgang");
            }

            // Cleanup-Timer für alte Cooldown-Einträge
            var cleanupTimer = new System.Timers.Timer(TimeSpan.FromHours(1).TotalMilliseconds);
            cleanupTimer.Elapsed += (sender, e) => _cooldownManager.CleanupOldEntries(TimeSpan.FromHours(24));
            cleanupTimer.Start();

            // Hauptschleife mit Budget-Überwachung und Average-Down
            int loopCounter = 0;
            while (true)
            {
                try
                {
                    loopCounter++;
                    var endTime = DateTime.UtcNow;
                    var startTime = endTime.AddMinutes(-30);

                    var volatileResults = new List<(string Symbol, decimal High, decimal Low, decimal DiffPercent, decimal Volume)>();
                    Log.Information($"🔍 Scanne nach volatilen Kryptowährungen... (Durchlauf #{loopCounter})");

                    var positions = _positionManager.GetPositions();
                    var myOwnAssets = await client.UnifiedApi.Account.GetAccountBalanceAsync();

                    // Erweiterte Budget-Überwachung bei jedem Durchlauf
                    if (myOwnAssets?.Data?.Details != null)
                    {
                        var currentEurBalance = myOwnAssets.Data.Details.FirstOrDefault(d => d.Asset == "EUR")?.AvailableBalance ?? 0m;

                        // Helper-Funktion für Preis-Abruf
                        async Task<decimal> GetCurrentPrice(string symbol)
                        {
                            try
                            {
                                var response = await client.UnifiedApi.ExchangeData.GetTickerAsync(symbol);
                                return response.Success ? (decimal)response.Data.LastPrice : 0m;
                            }
                            catch
                            {
                                return 0m;
                            }
                        }

                        _profitTracker.LogProfitStatus(currentEurBalance, positions, GetCurrentPrice);

                        if (loopCounter % 10 == 0)
                        {
                            var budgetStatus_1 = _profitTracker.GetBudgetStatus();
                            Log.Information($"🔄 Budget-Integrität geprüft (Durchlauf #{loopCounter}) - Verfügbar: {budgetStatus_1.AvailableBudget:F2} EUR");
                        }
                    }

                    foreach (var symbol in eurSymbols)
                    {
                        if (!_blacklistManager.IsSymbolAllowed(symbol))
                        {
                            continue;
                        }

                        var klinesResponse = await client.UnifiedApi.ExchangeData.GetKlinesAsync(
                            symbol,
                            KlineInterval.OneMinute,
                            startTime,
                            endTime
                        );

                        if (!klinesResponse.Success || !klinesResponse.Data.Any())
                            continue;

                        var high = klinesResponse.Data.Max(k => k.HighPrice);
                        var low = klinesResponse.Data.Min(k => k.LowPrice);
                        var volume = klinesResponse.Data.Max(k => k.Volume);

                        if (low == 0 || high == 0)
                            continue;

                        var diffPercent = ((high - low) / low) * 100;

                        if (diffPercent >= 1.5m || volume > 100)
                            volatileResults.Add((symbol, high, low, diffPercent, volume));
                    }

                    var top = volatileResults
                        .OrderByDescending(r => r.DiffPercent)
                        .ThenByDescending(r => r.Volume)
                        .Where(r => r.DiffPercent >= 0.5m || r.Volume > 100)
                        .ToList();

                    top.ForEach(r => Log.Debug($"{r.Symbol} - {r.Volume} {r.DiffPercent}"));
                    Log.Information($"Top volatile Kryptowährungen für Trading: {top.Count}");

                    // Erweiterte Status-Anzeige mit Average-Down Info
                    var activeLockouts = _cooldownManager.GetActiveLockouts();
                    var budgetStatus = _profitTracker.GetBudgetStatus();

                    Log.Information($"💰 Budget: {budgetStatus.AvailableBudget:F2} EUR verfügbar | {budgetStatus.TotalInvested:F2} EUR investiert | {budgetStatus.TotalProfit:F2} EUR Profit");

                    if (activeLockouts.Any())
                    {
                        Log.Information($"🚫 Gesperrte Symbole ({activeLockouts.Count}):");
                        foreach (var lockout in activeLockouts)
                        {
                            Log.Information($"   {lockout.Key}: noch {lockout.Value.TotalMinutes:F1} Min gesperrt");
                        }
                    }

                    // Aktive Positionen mit Average-Down Status anzeigen
                    var currentPositions = _positionManager.GetPositions();
                    if (currentPositions.Any())
                    {
                        Log.Information($"📈 Aktive Positionen ({currentPositions.Count}):");
                        foreach (var pos in currentPositions)
                        {
                            var tickerResponse = await client.UnifiedApi.ExchangeData.GetTickerAsync(pos.Symbol);
                            if (tickerResponse.Success)
                            {
                                var currentPrice = tickerResponse.Data.LastPrice;
                                pos.CurrentMarketPrice = (decimal)currentPrice;
                                var (unrealizedPL, unrealizedPLPercent) = pos.CalculateUnrealizedPL((decimal)currentPrice);

                                var profitSymbol = unrealizedPL >= 0 ? "📈" : "📉";
                                var avgDownInfo = pos.AverageDownCount > 0 ? $" [AvgDown: {pos.AverageDownCount}/3]" : "";
                                var triggerInfo = pos.AverageDownEnabled ? $" [Trigger: {pos.NextAverageDownTrigger:F4}]" : " [AvgDown: OFF]";

                                Log.Information($"   {profitSymbol} {pos.Symbol}: {unrealizedPL:F2} EUR ({unrealizedPLPercent:F2}%) | Ziel: {pos.High:F4}{avgDownInfo}{triggerInfo}");
                            }
                        }
                    }

                    var datetimeNow = DateTime.UtcNow;
                    var processed = new List<(string Symbol, decimal High, decimal Low, decimal DiffPercent, decimal Volume)>();

                    // Trading-Schleife mit Budget-Kontrolle und Average-Down
                    while (DateTime.UtcNow.Subtract(datetimeNow).TotalMinutes < 1)
                    {
                        // Budget-Check vor jeder Trading-Iteration
                        var currentBudgetStatus = _profitTracker.GetBudgetStatus();
                        if (currentBudgetStatus.AvailableBudget < 50m)
                        {
                            Log.Information($"⚠️ Trading pausiert: Budget unter 50 EUR ({currentBudgetStatus.AvailableBudget:F2} EUR verfügbar)");
                            break;
                        }

                        foreach (var entry in top)
                        {
                            if (!_blacklistManager.IsSymbolAllowed(entry.Symbol))
                            {
                                processed.Add(entry);
                                continue;
                            }
                            Log.Debug($"check entry {entry.Symbol} ...");
                            // NEUE METHODE: BuySellSymbolWithAverageDown
                            var ret = await BuySellSymbolWithAverageDown(stack, (double)entry.Low, (double)entry.High, client, entry.Symbol);
                            if (ret)
                            {
                                processed.Add(entry);
                                Log.Information($"Kauf/Verkauf/Average-Down erfolgreich für {entry.Symbol}");

                                var newBudgetStatus = _profitTracker.GetBudgetStatus();
                                Log.Information($"💰 Neuer Budget-Status: {newBudgetStatus.AvailableBudget:F2} EUR verfügbar");
                            }

                            await Task.Delay(100);
                        }

                        await CheckAndExecuteSells(client);
                        await Task.Delay(2000);
                        top = top.Except(processed).ToList();
                    }

                    Log.Information($"=== 📊 REGELMÄSSIGER BUDGET-BERICHT MIT AVERAGE-DOWN (Durchlauf #{loopCounter}) ===");
                    ShowBudgetStatus();
                    ShowActivePositionsDetailed(client);

                    if (DateTime.UtcNow.Minute % 10 == 0)
                    {
                        _cooldownManager.CleanupOldEntries(TimeSpan.FromHours(1));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Fehler in Hauptschleife: {ex.Message}");
                    await Task.Delay(5000);
                }
            }
        }

        // NEUE METHODE: BuySellSymbol mit Average-Down Integration
        private static async Task<bool> BuySellSymbolWithAverageDown(AutoTrimStack<double> stack, double untereSchranke, double obereSchranke, OKXRestClient client, string symbol)
        {
            try
            {
                if (!_blacklistManager.IsSymbolAllowed(symbol))
                {
                    return false;
                }

                var response = await client.UnifiedApi.ExchangeData.GetTickerAsync(symbol);
                if (!response.Success)
                {
                    return false;
                }

                var currentPrice = response.Data.LastPrice;
                var existingPosition = _positionManager.GetPositionBySymbol(symbol);

                // FALL 1: Prüfe Average-Down für bestehende Position
                if (existingPosition != null)
                {
                    if (existingPosition.ShouldTriggerAverageDown((decimal)currentPrice))
                    {
                        if (!_cooldownManager.CanBuy(symbol))
                        {
                            Log.Debug($"Average-Down für {symbol} durch Cooldown blockiert");
                            return false;
                        }

                        // Berechne Investment für Average-Down (gleiches Investment wie Original)
                        var originalInvestmentAmount = existingPosition.TotalInvestedAmount / existingPosition.AverageDownCount + 1;
                        var additionalInvestment = originalInvestmentAmount;

                        Log.Information($"=== AVERAGE-DOWN VORBEREITUNG ===");
                        Log.Information($"Symbol: {symbol}");
                        Log.Information($"Aktueller Preis: {currentPrice:F6} EUR");
                        Log.Information($"Durchschnittspreis: {existingPosition.PurchasePrice:F6} EUR");
                        Log.Information($"Zusätzliches Investment: {additionalInvestment:F2} EUR");

                        if (!_profitTracker.CanAffordPurchaseStrict(additionalInvestment, $"{symbol}_AVGDOWN"))
                        {
                            Log.Warning($"❌ Average-Down für {symbol} abgelehnt - Budget nicht ausreichend");
                            existingPosition.DisableAverageDown("Budget nicht ausreichend");
                            return false;
                        }

                        if (!_profitTracker.ReserveBudgetForPurchase(additionalInvestment, $"{symbol}_AVGDOWN"))
                        {
                            Log.Error($"❌ Budget-Reservierung für Average-Down {symbol} fehlgeschlagen!");
                            return false;
                        }

                        var buyResponse = await client.UnifiedApi.Trading.PlaceOrderAsync(
                            symbol,
                            OrderSide.Buy,
                            OrderType.Market,
                            tradeMode: TradeMode.Cash,
                            quantity: additionalInvestment);

                        if (buyResponse.Success)
                        {
                            _cooldownManager.RecordBuy(symbol);

                            // Average-Down in Position durchführen
                            var newAveragePrice = existingPosition.ExecuteAverageDown((decimal)currentPrice, additionalInvestment);

                            // WICHTIG: Verkaufsziel bleibt unverändert!
                            // existingPosition.High wird NICHT verändert - das ursprüngliche Ziel bleibt bestehen

                            var budgetStatus = _profitTracker.GetBudgetStatus();
                            Log.Information($"✅ AVERAGE-DOWN: {symbol} | {additionalInvestment:F2} EUR | Budget: {budgetStatus.AvailableBudget:F2} EUR verbleibend");
                            Log.Information($"🎯 Verkaufsziel bleibt bei: {existingPosition.High:F6} EUR");

                            return true;
                        }
                        else
                        {
                            Log.Error($"❌ Average-Down Order für {symbol} fehlgeschlagen: {buyResponse.Error}");
                            _profitTracker.ReleaseBudgetFromSale(additionalInvestment, 0, $"{symbol}_AVGDOWN_FAILED");
                            return false;
                        }
                    }
                    return false; // Position existiert, aber kein Average-Down nötig
                }

                // FALL 2: Neue Position erstellen (bestehende Logik)
                var candles = await client.UnifiedApi.ExchangeData.GetKlinesAsync(symbol, KlineInterval.OneMinute);
                if (!candles.Success || !candles.Data.Any())
                {
                    return false;
                }

                var closes = candles.Data.OrderByDescending(r => r.Time).Select(c => (double)c.ClosePrice).ToList();
                var price = closes.FirstOrDefault();
                if (price == 0)
                    return false;

                ITradingStrategy strategy = new EmaBollingerStrategy();

                if (!_cooldownManager.CanBuy(symbol))
                {
                    return false;
                }

                bool shouldBuy = strategy.ShouldBuy(closes, (decimal)currentPrice);// (*)&& (
                                                                                   //price <= (closes.Min() * 1.005));

                if (shouldBuy)
                {
                    if (!InstrumentInfos.TryGetValue(symbol, out var minOrderSize))
                    {
                        return false;
                    }

                    var finalOrderSize = minOrderSize.MinimumOrderSize;
                    var requiredEurAmount = finalOrderSize * currentPrice * 10 * 1.02m;

                    Log.Information($"=== NEUE POSITION - KAUF-VORBEREITUNG ===");
                    Log.Information($"Symbol: {symbol}");
                    Log.Information($"Benötigtes Investment: {requiredEurAmount:F2} EUR");

                    if (!_profitTracker.CanAffordPurchaseStrict((decimal)requiredEurAmount, symbol))
                    {
                        Log.Information($"❌ Kauf von {symbol} abgelehnt - Trading-Budget nicht ausreichend");
                        return false;
                    }

                    if (!_profitTracker.ReserveBudgetForPurchase((decimal)requiredEurAmount, symbol))
                    {
                        Log.Error($"❌ Budget-Reservierung für {symbol} fehlgeschlagen!");
                        return false;
                    }

                    var buyResponse = await client.UnifiedApi.Trading.PlaceOrderAsync(
                        symbol,
                        OrderSide.Buy,
                        OrderType.Market,
                        tradeMode: TradeMode.Cash,
                        quantity: (decimal)requiredEurAmount);

                    if (buyResponse.Success)
                    {
                        _cooldownManager.RecordBuy(symbol);

                        Log.Information($"=== ✅ NEUE POSITION ERSTELLT ===");
                        Log.Information($"Symbol: {symbol}");
                        Log.Information($"EUR-Investment: {requiredEurAmount:F2} EUR");

                        var newPosition = new EnhancedTradingPosition
                        {
                            Symbol = symbol,
                            High = (decimal)currentPrice * 1.007m,
                            Processed = DateTime.UtcNow,
                            OrderId = $"{buyResponse.Data?.OrderId}"
                        };

                        // Position mit Average-Down Funktionalität initialisieren
                        var volume = (double)(requiredEurAmount / currentPrice);
                        newPosition.InitializePosition((decimal)currentPrice, volume, (decimal)requiredEurAmount);

                        _positionManager.AddOrUpdatePosition(newPosition);

                        var budgetStatus = _profitTracker.GetBudgetStatus();
                        Log.Information($"🛒 NEUE POSITION: {symbol} | {requiredEurAmount:F2} EUR | Budget: {budgetStatus.AvailableBudget:F2} EUR verbleibend");
                        return true;
                    }
                    else
                    {
                        Log.Error($"❌ Kauf-Order für {symbol} fehlgeschlagen: {buyResponse.Error}");
                        _profitTracker.ReleaseBudgetFromSale((decimal)requiredEurAmount, 0, $"{symbol}_FAILED");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Fehler in BuySellSymbolWithAverageDown für {symbol}: {ex.Message}");
            }

            return false;
        }

        private static async Task CheckAndExecuteSells(OKXRestClient client)
        {
            var positions = _positionManager.GetPositions();
            var positionsToRemove = new List<TradingPosition>();

            foreach (var position in positions)
            {
                if (!_blacklistManager.IsSymbolAllowed(position.Symbol))
                {
                    Log.Warning($"Position {position.Symbol} kann nicht verkauft werden: Symbol ist geblacklisted");
                    continue;
                }

                if (!_cooldownManager.CanSell(position.Symbol))
                    continue;

                var response = await client.UnifiedApi.ExchangeData.GetTickerAsync(position.Symbol);
                if (response.Success && response.Data != null)
                {
                    var currentPrice = response.Data.LastPrice;

                    // Verkaufen wenn Ziel erreicht oder bei Gewinn
                    if (currentPrice > position.High || position.CanSell((decimal)currentPrice))
                    {
                        var assets = await client.UnifiedApi.Account.GetAccountBalanceAsync();
                        var assetName = position.Symbol.Split('-')[0];
                        var asset = assets.Data?.Details?.FirstOrDefault(d => d.Asset == assetName);

                        if (asset != null && asset.Asset != "EUR" && asset.AvailableBalance > 0)
                        {
                            var actualAvailableBalance = asset.AvailableBalance.Value;

                            var sellResponse = await client.UnifiedApi.Trading.PlaceOrderAsync(
                                position.Symbol,
                                OrderSide.Sell,
                                OrderType.Limit,
                                tradeMode: TradeMode.Cash,
                                quantity: actualAvailableBalance,
                                price: currentPrice);

                            if (sellResponse.Success)
                            {
                                _cooldownManager.RecordSell(position.Symbol);

                                var originalInvestmentEUR = position.TotalInvestedAmount; // Gesamtinvestment inkl. Average-Down
                                var actualSaleValueEUR = currentPrice * actualAvailableBalance;
                                var actualProfit = actualSaleValueEUR - originalInvestmentEUR;

                                _profitTracker.ReleaseBudgetFromSale(originalInvestmentEUR, (decimal)actualSaleValueEUR, position.Symbol);

                                positionsToRemove.Add(position);

                                Log.Information($"=== ✅ VERKAUF ERFOLGREICH (MIT AVERAGE-DOWN) ===");
                                Log.Information($"Symbol: {position.Symbol}");
                                Log.Information($"Durchschnittspreis: {position.PurchasePrice:F6} EUR (Original: {position.OriginalPurchasePrice:F6})");
                                Log.Information($"Verkauft bei: {currentPrice:F6} EUR");
                                Log.Information($"Gesamtinvestment: {originalInvestmentEUR:F2} EUR");
                                Log.Information($"Verkaufswert: {actualSaleValueEUR:F2} EUR");
                                Log.Information($"Profit/Verlust: {actualProfit:F2} EUR");
                                if (position.AverageDownCount > 0)
                                {
                                    Log.Information($"Average-Down Käufe: {position.AverageDownCount}");
                                    Log.Information($"Preis-Verbesserung: {((position.OriginalPurchasePrice - position.PurchasePrice) / position.OriginalPurchasePrice * 100):F2}%");
                                }

                                var budgetStatus = _profitTracker.GetBudgetStatus();
                                Log.Information($"✅ Verkauf: {position.Symbol} | Profit: {actualProfit:F2} EUR | Budget: {budgetStatus.AvailableBudget:F2} EUR verfügbar");
                            }
                            else
                            {
                                Log.Error($"Verkaufsfehler {position.Symbol}: {sellResponse.Error}");
                            }
                        }
                    }
                }
            }

            foreach (var position in positionsToRemove)
            {
                _positionManager.RemovePosition(position);
            }
        }

        // Hilfsmethoden für Budget-Management
        public static void ShowBudgetStatus()
        {
            var budgetStatus = _profitTracker.GetBudgetStatus();

            Log.Information("=== 💰 AKTUELLER BUDGET-STATUS ===");
            Log.Information($"Verfügbares Trading-Budget: {budgetStatus.AvailableBudget:F2} EUR");
            Log.Information($"Aktuell investiert: {budgetStatus.TotalInvested:F2} EUR");
            Log.Information($"Realisierter Profit: {budgetStatus.TotalProfit:F2} EUR");
            Log.Information($"Initial-Balance: {budgetStatus.InitialBalance:F2} EUR");
            Log.Information($"Budget-Auslastung: {(budgetStatus.TotalInvested / budgetStatus.InitialBalance * 100):F1}%");
        }

        public static void ShowActivePositions()
        {
            var positions = _positionManager.GetPositions();
            var count = _positionManager.GetPositionCount();

            Log.Information($"=== AKTIVE POSITIONEN ({count}) ===");
            if (positions.Any())
            {
                foreach (var pos in positions)
                {
                    var asset = pos.Symbol.Split('-')[0];
                    Log.Information($"📈 {asset}: {pos.Symbol} | Ziel: {pos.High:F6} EUR | Gekauft: {pos.PurchasePrice:F6} EUR");
                    if (pos.AverageDownCount > 0)
                    {
                        Log.Information($"   🔄 Average-Down: {pos.AverageDownCount}/3 | Original: {pos.OriginalPurchasePrice:F6} EUR");
                    }
                }
            }
            else
            {
                Log.Information("Keine aktiven Positionen");
            }
        }

        // NEUE METHODE: Detaillierte Positions-Anzeige mit Average-Down Informationen
        public static async void ShowActivePositionsDetailed(OKXRestClient client)
        {
            var positions = _positionManager.GetPositions();
            var count = _positionManager.GetPositionCount();

            Log.Information($"=== 📊 DETAILLIERTE POSITIONEN MIT AVERAGE-DOWN ({count}) ===");

            if (!positions.Any())
            {
                Log.Information("Keine aktiven Positionen");
                return;
            }

            foreach (var pos in positions)
            {
                try
                {
                    var tickerResponse = await client.UnifiedApi.ExchangeData.GetTickerAsync(pos.Symbol);
                    if (tickerResponse.Success)
                    {
                        var currentPrice = tickerResponse.Data.LastPrice;
                        Log.Information(pos.GetDetailedInfo((decimal)currentPrice));
                    }
                    else
                    {
                        Log.Warning($"Konnte Preis für {pos.Symbol} nicht abrufen");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Fehler beim Abrufen der Position-Details für {pos.Symbol}: {ex.Message}");
                }
            }
        }

        // NEUE METHODE: Average-Down Status für alle Positionen
        public static void ShowAverageDownStatus()
        {
            var positions = _positionManager.GetPositions();
            var avgDownPositions = positions.Where(p => p.AverageDownCount > 0 || p.AverageDownEnabled).ToList();

            Log.Information($"=== 🔄 AVERAGE-DOWN STATUS ===");

            if (!avgDownPositions.Any())
            {
                Log.Information("Keine Positionen mit Average-Down aktiviert");
                return;
            }

            foreach (var pos in avgDownPositions)
            {
                var status = pos.AverageDownEnabled ? "AKTIV" : "DEAKTIVIERT";
                var color = pos.AverageDownEnabled ? "🟢" : "🔴";

                Log.Information($"{color} {pos.Symbol}: {status} | Käufe: {pos.AverageDownCount}/3 | Trigger: {pos.NextAverageDownTrigger:F6} EUR");

                if (pos.AverageDownHistory.Any())
                {
                    Log.Information($"   Historie: {string.Join(", ", pos.AverageDownHistory.Select(h => $"{h.Price:F4}@{h.Timestamp:HH:mm}"))}");
                }
            }
        }

        // NEUE METHODE: Manuelle Average-Down Deaktivierung
        public static void DisableAverageDownForSymbol(string symbol, string reason = "Manuell deaktiviert")
        {
            var position = _positionManager.GetPositionBySymbol(symbol);
            if (position != null)
            {
                position.DisableAverageDown(reason);
                Log.Information($"Average-Down für {symbol} wurde manuell deaktiviert");
            }
            else
            {
                Log.Warning($"Keine Position für {symbol} gefunden");
            }
        }

        // NEUE METHODE: Average-Down für alle Positionen deaktivieren
        public static void DisableAllAverageDowns(string reason = "Global deaktiviert")
        {
            var positions = _positionManager.GetPositions();
            var count = 0;

            foreach (var pos in positions.Where(p => p.AverageDownEnabled))
            {
                pos.DisableAverageDown(reason);
                count++;
            }

            Log.Information($"Average-Down für {count} Positionen deaktiviert. Grund: {reason}");
        }

        // NEUE METHODE: Statistiken über Average-Down Performance
        public static void ShowAverageDownStatistics()
        {
            var positions = _positionManager.GetPositions();
            var avgDownPositions = positions.Where(p => p.AverageDownCount > 0).ToList();

            if (!avgDownPositions.Any())
            {
                Log.Information("=== 📈 AVERAGE-DOWN STATISTIKEN ===");
                Log.Information("Keine Positionen mit Average-Down Käufen vorhanden");
                return;
            }

            var totalAvgDowns = avgDownPositions.Sum(p => p.AverageDownCount);
            var avgPriceImprovement = avgDownPositions.Average(p =>
                ((p.OriginalPurchasePrice - p.PurchasePrice) / p.OriginalPurchasePrice * 100));

            Log.Information("=== 📈 AVERAGE-DOWN STATISTIKEN ===");
            Log.Information($"Positionen mit Average-Down: {avgDownPositions.Count}");
            Log.Information($"Gesamt Average-Down Käufe: {totalAvgDowns}");
            Log.Information($"Durchschnittliche Preis-Verbesserung: {avgPriceImprovement:F2}%");

            Log.Information("--- Details pro Position ---");
            foreach (var pos in avgDownPositions)
            {
                var improvement = ((pos.OriginalPurchasePrice - pos.PurchasePrice) / pos.OriginalPurchasePrice * 100);
                Log.Information($"{pos.Symbol}: {pos.AverageDownCount} Käufe, {improvement:F2}% Verbesserung");
            }
        }
    }
}