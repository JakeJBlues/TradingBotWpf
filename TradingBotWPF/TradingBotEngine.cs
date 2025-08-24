using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OKX.Net;
using OKX.Net.Enums;
using OKX.Net.Objects;
using OKX.Net.Objects.Public;
using CryptoExchange.Net.Authentication;
using Serilog;
using OKX.Net.Clients;
using TradingBotWPF;
using OKX.Net.Objects.Account;
using System.IO;
using TradingBotWPF.Interfaces;
using TradingBotWPF.Manager;
using TradingBotWPF.Entities;
using TradingBotWPF.Strategies;
using TradingBotWPF.Helper;

namespace TradingBot
{
    /// <summary>
    /// Vollständiger TradingBot mit 30-Tage-High Filter, RSI-30Min Filter und SubAccount Transfer-Funktionen
    /// </summary>
    public class TradingBotEngine
    {
        private const decimal Val1 = 750m;
        #region Private Fields
        private readonly TradingCooldownManager _cooldownManager;
        private readonly PositionManager _positionManager;
        private readonly ProtectedProfitTracker _profitTracker;
        private readonly TradingBlacklistManager _blacklistManager;

        private OKXRestClient _client;
        private Dictionary<string, OKXInstrument> _instrumentInfos;
        private List<string> _eurSymbols;
        private bool _isRunning = false;
        private TradingBotConfiguration _configuration;

        // Trading Strategy
        private readonly ITradingStrategy _strategy;
        private readonly AutoTrimStack<double> _priceStack;

        // 30-Tage-High Filter Konfiguration
        private readonly decimal _thirtyDayHighThreshold = 0.95m; // 90% des 30-Tage-Highs
        private readonly bool _enableThirtyDayHighFilter = true; // Filter aktiviert

        // RSI Filter Konfiguration (30 Minuten Basis)
        private readonly decimal _rsiThreshold = 70m; // RSI Schwellenwert
        private readonly bool _enableRsiFilter = true; // RSI Filter aktiviert
        private readonly int _rsiPeriod = 14; // RSI Periode (Standard: 14)
        public bool ShouldBuy { get; set; } = true; // Flag für Kaufentscheidungen
        #endregion

        #region Constructor
        public TradingBotEngine(
            TradingCooldownManager cooldownManager,
            PositionManager positionManager,
            ProtectedProfitTracker profitTracker,
            TradingBlacklistManager blacklistManager)
        {
            _cooldownManager = cooldownManager;
            _positionManager = positionManager;
            _profitTracker = profitTracker;
            _blacklistManager = blacklistManager;

            _strategy = new EmaBollingerStrategy();
            _priceStack = new AutoTrimStack<double>(5);
            _instrumentInfos = new Dictionary<string, OKXInstrument>();
            _eurSymbols = new List<string>();
        }
        #endregion

        #region Public Methods
        public async Task ConfigureAsync(TradingBotConfiguration configuration)
        {
            _configuration = configuration;

            // OKX Client konfigurieren
            _client = Login.Credentials();

            // Symbole laden und filtern
            await LoadAndFilterSymbolsAsync();

            Log.Information("🔧 TradingBot Engine konfiguriert");
            Log.Information($"🛡️ 30-Tage-High Filter: {(_enableThirtyDayHighFilter ? "AKTIV" : "INAKTIV")} (Schwelle: {_thirtyDayHighThreshold * 100:F0}%)");
            Log.Information($"📊 RSI-30Min Filter: {(_enableRsiFilter ? "AKTIV" : "INAKTIV")} (Schwelle: {_rsiThreshold}, Periode: {_rsiPeriod})");
        }



        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_isRunning)
            {
                Log.Warning("Bot läuft bereits!");
                return;
            }

            _isRunning = true;
            Log.Information("🚀 TradingBot Engine gestartet");

            try
            {
                // Initial-Setup
                await PerformInitialSetupAsync();

                // Hauptschleife
                await RunTradingLoopAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Log.Information("🛑 TradingBot durch Benutzer gestoppt");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Kritischer Fehler in TradingBot Engine");
                throw;
            }
            finally
            {
                _isRunning = false;
                Log.Information("🏁 TradingBot Engine beendet");
            }
        }

        public async Task SellAllPositions()
        {
            var positions = _positionManager.GetPositions();
            foreach (var position in positions)
            {
                ExecuteSellOrderAsync(position, -1);
                _positionManager.RemovePosition(position);
            }
        }

        public async Task StopAsync()
        {
            _isRunning = false;
            Log.Information("🛑 Stopp-Signal an TradingBot Engine gesendet");
        }

        public bool IsRunning => _isRunning;

        /// <summary>
        /// Transferiert einen Betrag in EUR vom Hauptkonto zu einem SubAccount
        /// </summary>
        public async Task<bool> TransferToSubAccountAsync(decimal amountEur, string subAccountName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(subAccountName))
                {
                    Log.Error("❌ SubAccount Name darf nicht leer sein");
                    return false;
                }

                if (amountEur <= 0)
                {
                    Log.Error($"❌ Transfer-Betrag muss positiv sein: {amountEur}");
                    return false;
                }

                Log.Information($"=== 💸 SUBACCOUNT TRANSFER VORBEREITUNG ===");
                Log.Information($"Betrag: {amountEur:F2} EUR");
                Log.Information($"Ziel SubAccount: {subAccountName}");

                // 1. Aktuelle EUR Balance prüfen
                var balanceResponse = await _client.UnifiedApi.Account.GetAccountBalanceAsync();
                if (!balanceResponse.Success || balanceResponse.Data?.Details == null)
                {
                    Log.Error("❌ Konnte Konto-Balance nicht abrufen");
                    return false;
                }

                var eurBalance = balanceResponse.Data.Details
                    .FirstOrDefault(d => d.Asset == "EUR")?.AvailableBalance ?? 0m;

                if (eurBalance < amountEur)
                {
                    Log.Error($"❌ Unzureichende EUR Balance: {eurBalance:F2} EUR verfügbar, {amountEur:F2} EUR benötigt");
                    return false;
                }

                Log.Information($"💰 Verfügbare EUR Balance: {eurBalance:F2} EUR");

                // 2. SubAccount existiert prüfen (optional - falls API verfügbar)
                try
                {
                    var subAccountsResponse = await _client.UnifiedApi.SubAccounts.GetSubAccountsAsync();
                    if (subAccountsResponse.Success && subAccountsResponse.Data?.Any() == true)
                    {
                        var subAccountExists = subAccountsResponse.Data.Any(sub =>
                            sub.SubAccountName?.Equals(subAccountName, StringComparison.OrdinalIgnoreCase) == true);

                        if (!subAccountExists)
                        {
                            Log.Warning($"⚠️ SubAccount '{subAccountName}' nicht in der Liste gefunden - Transfer wird trotzdem versucht");
                        }
                        else
                        {
                            Log.Information($"✅ SubAccount '{subAccountName}' gefunden");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug($"SubAccount-Validierung übersprungen: {ex.Message}");
                }

                // 3. Transfer ausführenU
                Log.Information("🚀 Führe Transfer aus...");

                var transferResponse = await _client.UnifiedApi.Account.TransferAsync("EUR", amountEur, TransferType.MasterAccountToSubAccount, AccountType.Trading, AccountType.Trading, subAccountName);

                if (transferResponse.Success)
                {
                    var transferId = $"{transferResponse.Data?.TransferId}";

                    Log.Information($"=== ✅ TRANSFER ERFOLGREICH ===");
                    Log.Information($"Transfer ID: {transferId}");
                    Log.Information($"Betrag: {amountEur:F2} EUR");
                    Log.Information($"Von: Hauptkonto");
                    Log.Information($"Zu: {subAccountName}");

                    // 4. Neue Balance nach Transfer loggen
                    await LogBalanceAfterTransferAsync(amountEur);

                    return true;
                }
                else
                {
                    Log.Error($"❌ Transfer fehlgeschlagen: {transferResponse.Error?.Message ?? "Unbekannter Fehler"}");
                    Log.Error($"Error Code: {transferResponse.Error?.Code}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"❌ Kritischer Fehler beim SubAccount Transfer");
                return false;
            }
        }

        /// <summary>
        /// Transferiert einen Prozentsatz der verfügbaren EUR Balance zu einem SubAccount
        /// </summary>
        public async Task<bool> TransferPercentageToSubAccountAsync(decimal percentage, string subAccountName)
        {
            try
            {
                if (percentage <= 0 || percentage > 100)
                {
                    Log.Error($"❌ Prozentsatz muss zwischen 0 und 100 liegen: {percentage}%");
                    return false;
                }

                // Aktuelle Balance abrufen
                var balanceResponse = await _client.UnifiedApi.Account.GetAccountBalanceAsync();
                if (!balanceResponse.Success || balanceResponse.Data?.Details == null)
                {
                    Log.Error("❌ Konnte Konto-Balance nicht abrufen");
                    return false;
                }

                var eurBalance = balanceResponse.Data.Details
                    .FirstOrDefault(d => d.Asset == "EUR")?.AvailableBalance ?? 0m;

                if (eurBalance <= 0)
                {
                    Log.Error("❌ Keine EUR Balance verfügbar für Transfer");
                    return false;
                }

                var transferAmount = (eurBalance * percentage) / 100m;

                Log.Information($"📊 Transfer {percentage}% von {eurBalance:F2} EUR = {transferAmount:F2} EUR");

                return await TransferToSubAccountAsync(transferAmount, subAccountName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"❌ Fehler beim Prozent-Transfer");
                return false;
            }
        }

        /// <summary>
        /// Holt EUR-Balance vom SubAccount zurück zum Hauptkonto
        /// </summary>
        public async Task<bool> TransferFromSubAccountAsync(decimal amountEur, string subAccountName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(subAccountName))
                {
                    Log.Error("❌ SubAccount Name darf nicht leer sein");
                    return false;
                }

                if (amountEur <= 0)
                {
                    Log.Error($"❌ Transfer-Betrag muss positiv sein: {amountEur}");
                    return false;
                }

                Log.Information($"=== 🔄 SUBACCOUNT RÜCKHOLUNG ===");
                Log.Information($"Betrag: {amountEur:F2} EUR");
                Log.Information($"Von SubAccount: {subAccountName}");

                var transferResponse = await _client.UnifiedApi.SubAccounts.TransferBetweenSubAccountsAsync(
                    "EUR",                          // Währung
                    amountEur,                      // Betrag
                    AccountType.Trading,
                    AccountType.Trading,
                    "joerg.reck@peanuts-soft.de",
                    subAccountName                  // Ziel SubAccount
                );

                if (transferResponse.Success)
                {
                    var transferId = $"{transferResponse.Data?.TransferId}";

                    Log.Information($"=== ✅ RÜCKHOLUNG ERFOLGREICH ===");
                    Log.Information($"Transfer ID: {transferId}");
                    Log.Information($"Betrag: {amountEur:F2} EUR");
                    Log.Information($"Von: {subAccountName}");
                    Log.Information($"Zu: Hauptkonto");

                    return true;
                }
                else
                {
                    Log.Error($"❌ Rückholung fehlgeschlagen: {transferResponse.Error?.Message ?? "Unbekannter Fehler"}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"❌ Fehler bei SubAccount Rückholung");
                return false;
            }
        }

        /// <summary>
        /// Listet alle verfügbaren SubAccounts auf
        /// </summary>
        public async Task<List<string>> GetSubAccountListAsync()
        {
            try
            {
                var subAccountsResponse = await _client.UnifiedApi.SubAccounts.GetSubAccountsAsync();

                if (subAccountsResponse.Success && subAccountsResponse.Data?.Any() == true)
                {
                    var subAccountNames = subAccountsResponse.Data
                        .Where(sub => !string.IsNullOrWhiteSpace(sub.SubAccountName))
                        .Select(sub => sub.SubAccountName)
                        .ToList();

                    Log.Information($"📋 Verfügbare SubAccounts ({subAccountNames.Count}):");
                    foreach (var name in subAccountNames)
                    {
                        Log.Information($"   • {name}");
                    }

                    return subAccountNames;
                }
                else
                {
                    Log.Information("📋 Keine SubAccounts gefunden");
                    return new List<string>();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Fehler beim Abrufen der SubAccount-Liste");
                return new List<string>();
            }
        }
        #endregion

        #region Private Methods - Filter Functions
        /// <summary>
        /// Prüft ob der aktuelle Preis zu nah am 30-Tage-High liegt
        /// </summary>
        private async Task<bool> IsNear30DayHighAsync(string symbol, decimal currentPrice, decimal threshold = 0.99m)
        {
            try
            {
                if (!_enableThirtyDayHighFilter)
                    return false;

                var endTime = DateTime.UtcNow;
                var startTime = endTime.AddDays(-30);

                // 30-Tage Klines abrufen (täglich)
                var klinesResponse = await _client.UnifiedApi.ExchangeData.GetKlinesAsync(
                    symbol,
                    KlineInterval.OneMinute,
                    startTime,
                    endTime
                );

                if (!klinesResponse.Success || !klinesResponse.Data.Any())
                {
                    Log.Warning($"⚠️ Konnte 30-Tage Daten für {symbol} nicht abrufen");
                    return false; // Bei Unsicherheit nicht blockieren
                }

                // Höchstpreis der letzten 30 Tage ermitteln
                var thirtyDayHigh = klinesResponse.Data.Max(k => k.HighPrice);

                // Schwellenwert berechnen
                var priceThreshold = thirtyDayHigh * threshold;

                var isNearHigh = currentPrice > priceThreshold;

                if (isNearHigh)
                {
                    var percentageOfHigh = (currentPrice / thirtyDayHigh) * 100;
                    Log.Information($"🚫 {symbol}: Zu nah am 30T-High - Aktuell: {currentPrice:F6}, 30T-High: {thirtyDayHigh:F6} ({percentageOfHigh:F1}%)");
                }
                else
                {
                    var percentageOfHigh = (currentPrice / thirtyDayHigh) * 100;
                    Log.Debug($"✅ {symbol}: 30T-High OK - Aktuell bei {percentageOfHigh:F1}% des 30T-Highs");
                }

                return isNearHigh;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"❌ Fehler beim 30-Tage-High Check für {symbol}");
                return false; // Bei Fehler nicht blockieren
            }
        }

        /// <summary>
        /// Berechnet RSI für die letzten 30 Minuten und prüft Überkauft-Status
        /// </summary>
        private async Task<bool> IsRsiOverboughtAsync(string symbol)
        {
            try
            {
                if (!_enableRsiFilter)
                    return false;

                var endTime = DateTime.UtcNow;
                var startTime = endTime.AddMinutes(-30); // Exakt 30 Minuten für RSI-Berechnung

                // 1-Minuten Klines für die letzten 30 Minuten abrufen
                var klinesResponse = await _client.UnifiedApi.ExchangeData.GetKlinesAsync(
                    symbol,
                    KlineInterval.OneMinute,
                    startTime,
                    endTime
                );

                if (!klinesResponse.Success || !klinesResponse.Data.Any())
                {
                    Log.Warning($"⚠️ Konnte 30-Minuten Daten für RSI-Berechnung bei {symbol} nicht abrufen");
                    return false; // Bei Unsicherheit nicht blockieren
                }

                // Mindestens 15 Datenpunkte für sinnvolle RSI-Berechnung
                if (klinesResponse.Data.Count() < Math.Min(_rsiPeriod, 15))
                {
                    Log.Debug($"⚠️ Nicht genügend 30-Min-Daten für RSI bei {symbol} ({klinesResponse.Data.Count()} verfügbar)");
                    return false;
                }

                // Schlusskurse extrahieren (chronologisch sortieren)
                var prices = klinesResponse.Data
                    .OrderBy(k => k.Time)
                    .Select(k => (double)k.ClosePrice)
                    .ToList();

                // RSI berechnen mit den verfügbaren Daten (max. 30 Minuten)
                var actualPeriod = Math.Min(_rsiPeriod, prices.Count - 1);
                var rsi = CalculateRSI(prices, actualPeriod);

                var isOverbought = (rsi > (double)_rsiThreshold);

                if (isOverbought)
                {
                    Log.Information($"🚫 {symbol}: RSI-30Min überkauft - RSI: {rsi:F2} (Schwelle: {_rsiThreshold}, {prices.Count} Datenpunkte)");
                }
                else
                {
                    Log.Debug($"✅ {symbol}: RSI-30Min OK - RSI: {rsi:F2} (unter {_rsiThreshold}, {prices.Count} Datenpunkte)");
                }

                return isOverbought;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"❌ Fehler beim RSI-30Min Check für {symbol}");
                return false; // Bei Fehler nicht blockieren
            }
        }

        /// <summary>
        /// Berechnet den RSI (Relative Strength Index)
        /// </summary>
        private double CalculateRSI(List<double> prices, int period)
        {
            if (prices.Count < period + 1)
                return 50; // Neutral RSI wenn nicht genug Daten

            var gains = new List<double>();
            var losses = new List<double>();

            // Preis-Änderungen berechnen
            for (int i = 1; i < prices.Count; i++)
            {
                var change = prices[i] - prices[i - 1];
                gains.Add(change > 0 ? change : 0);
                losses.Add(change < 0 ? Math.Abs(change) : 0);
            }

            // Durchschnittliche Gewinne und Verluste für die ersten 'period' Werte
            var avgGain = gains.Take(period).Average();
            var avgLoss = losses.Take(period).Average();

            // Gleitende Durchschnitte für die restlichen Werte (Wilder's Smoothing)
            for (int i = period; i < gains.Count; i++)
            {
                avgGain = ((avgGain * (period - 1)) + gains[i]) / period;
                avgLoss = ((avgLoss * (period - 1)) + losses[i]) / period;
            }

            // RSI berechnen
            if (avgLoss == 0)
                return 100; // Keine Verluste = maximaler RSI

            var relativeStrength = avgGain / avgLoss;
            var rsi = 100 - (100 / (1 + relativeStrength));

            return rsi;
        }

        /// <summary>
        /// Zeigt Balance-Informationen nach Transfer an
        /// </summary>
        private async Task LogBalanceAfterTransferAsync(decimal transferredAmount)
        {
            try
            {
                await Task.Delay(2000); // Kurz warten bis Transfer verarbeitet wurde

                var balanceResponse = await _client.UnifiedApi.Account.GetAccountBalanceAsync();
                if (balanceResponse.Success && balanceResponse.Data?.Details != null)
                {
                    var eurBalance = balanceResponse.Data.Details
                        .FirstOrDefault(d => d.Asset == "EUR")?.AvailableBalance ?? 0m;

                    Log.Information($"💰 Neue Hauptkonto EUR Balance: {eurBalance:F2} EUR");

                    // Trading Budget aktualisieren falls der Profit Tracker verwendet wird
                    if (_profitTracker != null)
                    {
                        var budgetStatus = _profitTracker.GetBudgetStatus();
                        Log.Information($"📊 Verbleibendes Trading Budget: {budgetStatus.AvailableBudget:F2} EUR");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"Balance-Logging nach Transfer übersprungen: {ex.Message}");
            }
        }
        #endregion

        #region Private Methods - Original Trading Logic
        private async Task LoadAndFilterSymbolsAsync()
        {
            Log.Information("📊 Lade verfügbare Symbole...");

            var symbolsResponse = await _client.UnifiedApi.ExchangeData.GetSymbolsAsync(InstrumentType.Spot);
            if (!symbolsResponse.Success)
            {
                throw new Exception($"Fehler beim Laden der Symbole: {symbolsResponse.Error}");
            }

            // EUR-Symbole extrahieren
            var allEurSymbols = symbolsResponse.Data
                .Where(s => s.Symbol.Contains("EUR"))
                .Select(s => s.Symbol)
                .Distinct()
                .ToList();

            // Über Blacklist filtern
            _eurSymbols = _blacklistManager.FilterAllowedSymbols(allEurSymbols);

            // Instrument-Infos speichern
            _instrumentInfos = symbolsResponse.Data
                .Where(s => _eurSymbols.Contains(s.Symbol))
                .ToDictionary(s => s.Symbol, s => s);

            Log.Information($"✅ {allEurSymbols.Count} EUR-Symbole gefunden, {_eurSymbols.Count} nach Filterung erlaubt");
            _blacklistManager.LogBlacklistStatus();
        }

        private async Task PerformInitialSetupAsync()
        {
            Log.Information("🔧 Führe Initial-Setup durch...");

            // Balance abrufen
            var asset = await _client.UnifiedApi.Account.GetAccountBalanceAsync();
            if (asset?.Data?.Details == null)
            {
                throw new Exception("Konnte Konto-Balance nicht abrufen");
            }

            // Initial-Verkauf aller bestehenden Assets (außer EUR)
            await PerformInitialSelloffAsync(asset.Data.Details);

            // Trading-Budget setzen
            await Task.Delay(3000); // Warten bis Verkäufe abgeschlossen
            await SetTradingBudgetAsync();

            Log.Information("✅ Initial-Setup abgeschlossen");
        }

        private async Task PerformInitialSelloffAsync(OKXAccountBalanceDetail[] details)
        {
            Log.Information("💰 Führe Initial-Verkauf aller Assets durch...");

            foreach (var item in details)
            {
                var sym = _eurSymbols.FirstOrDefault(s => s.StartsWith(item.Asset, StringComparison.OrdinalIgnoreCase));

                if (sym != null && item.Asset != "EUR" && (item.AvailableBalance ?? 0m) > 0 &&
                    _instrumentInfos.ContainsKey($"{item.Asset}-EUR") &&
                    _blacklistManager.IsSymbolAllowed(sym))
                {
                    var sellResponse = await _client.UnifiedApi.Trading.PlaceOrderAsync(
                        sym,
                        OrderSide.Sell,
                        OrderType.Market,
                        tradeMode: TradeMode.Cash,
                        quantity: item.AvailableBalance ?? 0m);

                    if (sellResponse.Success)
                    {
                        Log.Information($"✅ Initial-Verkauf: {item.Asset} ({sym})");
                    }
                    else
                    {
                        Log.Error($"❌ Initial-Verkauf Fehler {item.Asset}: {sellResponse.Error}");
                    }
                }
                else if (item.Asset == "EUR")
                {
                    Log.Information($"💰 EUR-Balance: {(item.AvailableBalance ?? 0m):F2} EUR");
                }
            }
        }

        private async Task SetTradingBudgetAsync()
        {
            var finalAsset = await _client.UnifiedApi.Account.GetAccountBalanceAsync();
            if (finalAsset?.Data?.Details != null)
            {
                var eurBalance = finalAsset.Data.Details.FirstOrDefault(d => d.Asset == "EUR")?.AvailableBalance ?? 0m;
                eurBalance = Math.Min(Val1, eurBalance);
                _profitTracker.SetInitialBalance(eurBalance);

                Log.Information($"=== 💰 TRADING-BUDGET GESETZT ===");
                Log.Information($"Verfügbares Budget: {eurBalance:F2} EUR");
                Log.Information($"🔒 Profit-Schutz aktiviert");
                Log.Information($"🔄 Average-Down aktiviert: Max. 3 Nachkäufe bei 1% Rückgang");
            }
        }

        private async Task RunTradingLoopAsync(CancellationToken cancellationToken)
        {
            int loopCounter = 0;

            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    loopCounter++;
                    Log.Information($"🔍 Trading-Durchlauf #{loopCounter}");

                    // Budget-Status loggen
                    await LogBudgetStatusAsync();

                    // Volatile Kryptowährungen scannen
                    var volatileResults = await ScanForVolatileSymbolsAsync();

                    // Aktuelle Positionen anzeigen
                    LogCurrentPositionsStatus();

                    // Trading-Schleife ausführen
                    await ExecuteTradingCycleAsync(volatileResults, cancellationToken);

                    // Verkaufs-Checks
                    await CheckAndExecuteSellsAsync();

                    // Cleanup alle 10 Durchläufe
                    if (loopCounter % 10 == 0)
                    {
                        _cooldownManager.CleanupOldEntries(TimeSpan.FromHours(1));
                    }

                    // Kurze Pause zwischen Durchläufen
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "❌ Fehler in Trading-Schleife");
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
            }
        }

        private async Task<List<(string Symbol, decimal High, decimal Low, decimal DiffPercent, decimal Volume)>> ScanForVolatileSymbolsAsync()
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddMinutes(-30);
            var volatileResults = new List<(string Symbol, decimal High, decimal Low, decimal DiffPercent, decimal Volume)>();

            foreach (var symbol in _eurSymbols)
            {
                if (!_blacklistManager.IsSymbolAllowed(symbol))
                    continue;

                var klinesResponse = await _client.UnifiedApi.ExchangeData.GetKlinesAsync(
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

                if (diffPercent >= 0.5m || volume > 500)
                    volatileResults.Add((symbol, high, low, diffPercent, volume));
            }

            var topResults = volatileResults
                .OrderByDescending(r => r.DiffPercent)
                .ThenByDescending(r => r.Volume)
                .Where(r => r.DiffPercent >= 0.5m || r.Volume > 500)
                .Take(25) // Top 15 volatile Symbole
                .ToList();

            Log.Information($"📈 {topResults.Count} volatile Symbole gefunden");
            return topResults;
        }

        private async Task ExecuteTradingCycleAsync(
            List<(string Symbol, decimal High, decimal Low, decimal DiffPercent, decimal Volume)> volatileResults,
            CancellationToken cancellationToken)
        {
            var cycleStartTime = DateTime.UtcNow;
            var processed = new List<(string Symbol, decimal High, decimal Low, decimal DiffPercent, decimal Volume)>();

            // Trading-Schleife für 1 Minute
            while (DateTime.UtcNow.Subtract(cycleStartTime).TotalMinutes < 1 && !cancellationToken.IsCancellationRequested)
            {
                // Budget-Check vor Trading-Iteration
                var budgetStatus = _profitTracker.GetProtectedBudgetStatus();
                if (budgetStatus.AvailableTradingBudget < 50m)
                {
                    Log.Information($"⚠️ Trading pausiert: Budget unter 50 EUR ({budgetStatus.AvailableTradingBudget:F2} EUR)");
                    ShouldBuy = true; // Kaufentscheidungen erlauben
                    break;
                }

                foreach (var entry in volatileResults.Except(processed))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (!_blacklistManager.IsSymbolAllowed(entry.Symbol))
                    {
                        processed.Add(entry);
                        continue;
                    }

                    // Trading-Entscheidung mit Average-Down
                    var success = await BuySellSymbolWithAverageDownAsync(entry.Symbol, entry.Low, entry.High);
                    if (success)
                    {
                        processed.Add(entry);
                        Log.Information($"✅ Trading-Aktion für {entry.Symbol} erfolgreich");

                        var newBudgetStatus = _profitTracker.GetBudgetStatus();
                        Log.Information($"💰 Budget-Update: {newBudgetStatus.AvailableBudget:F2} EUR verfügbar");
                    }

                    await Task.Delay(100, cancellationToken);
                }

                // Verkaufs-Checks während Trading-Zyklus
                await CheckAndExecuteSellsAsync();
                await Task.Delay(2000, cancellationToken);

                // Aktualisiere volatile Liste (entferne verarbeitete)
                volatileResults = volatileResults.Except(processed).ToList();
            }
        }

        private async Task<bool> BuySellSymbolWithAverageDownAsync(string symbol, decimal low, decimal high)
        {
            try
            {
                if (!_blacklistManager.IsSymbolAllowed(symbol))
                    return false;

                var response = await _client.UnifiedApi.ExchangeData.GetTickerAsync(symbol);
                if (!response.Success)
                    return false;

                var currentPrice = response.Data.LastPrice;
                var existingPosition = _positionManager.GetPositionBySymbol(symbol);

                // FALL 1: Average-Down für bestehende Position
                //if (existingPosition != null)
                //{
                //    return await HandleAverageDownAsync(existingPosition, (decimal)currentPrice, symbol);
                //}

                // FALL 2: Neue Position erstellen
                if (_positionManager.HasPositionForAsset(symbol))
                {
                    Log.Debug($"Position für {symbol} existiert bereits, überspringe Kauf");
                    return false;
                }
                return await HandleNewPositionAsync(symbol, (decimal)currentPrice);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"❌ Fehler in BuySellSymbolWithAverageDown für {symbol}");
                return false;
            }
        }

        private async Task<bool> HandleAverageDownAsync(TradingPosition existingPosition, decimal currentPrice, string symbol)
        {
            return false;
            if (!existingPosition.ShouldTriggerAverageDown(currentPrice))
                return false;

            if (!_cooldownManager.CanBuy(symbol))
            {
                Log.Debug($"Average-Down für {symbol} durch Cooldown blockiert");
                return false;
            }

            // Investment für Average-Down berechnen
            var originalInvestmentAmount = existingPosition.TotalInvestedAmount / (existingPosition.AverageDownCount + 1);
            var additionalInvestment = originalInvestmentAmount;

            Log.Information($"=== 🔄 AVERAGE-DOWN VORBEREITUNG ===");
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

            var buyResponse = await _client.UnifiedApi.Trading.PlaceOrderAsync(
                symbol,
                OrderSide.Buy,
                OrderType.Market,
                tradeMode: TradeMode.Cash,
                quantity: additionalInvestment);

            var orderId = buyResponse.Data?.OrderId;
            Log.Information($"✅ Order platziert - Order ID: {orderId}");

            // Tatsächlichen Preis aus Order Details holen
            var actualPrice = await OrderPriceHelper.GetActualPriceFromOrderAsync(_client, $"{orderId}");

            if (actualPrice > 0)
            {
                Log.Information($"💰 Tatsächlicher Kaufpreis: {actualPrice:F6} EUR (via Order Details)");
            }
            else
            {
                Log.Warning($"⚠️ Konnte tatsächlichen Preis nicht ermitteln für Order {orderId}");
            }

            if (buyResponse.Success)
            {
                _cooldownManager.RecordBuy(symbol);

                // Average-Down ausführen
                var newAveragePrice = existingPosition.ExecuteAverageDown(actualPrice, additionalInvestment);

                var budgetStatus = _profitTracker.GetBudgetStatus();
                Log.Information($"✅ AVERAGE-DOWN: {symbol} | {additionalInvestment:F2} EUR | Budget: {budgetStatus.AvailableBudget:F2} EUR");
                Log.Information($"🎯 Verkaufsziel bleibt: {existingPosition.High:F6} EUR");

                return true;
            }
            else
            {
                Log.Error($"❌ Average-Down Order für {symbol} fehlgeschlagen: {buyResponse.Error}");
                _profitTracker.ReleaseBudgetFromSale(additionalInvestment, 0, $"{symbol}_AVGDOWN_FAILED");
                return false;
            }
        }

        private async Task<bool> HandleNewPositionAsync(string symbol, decimal currentPrice)
        {
            // ✅ 30-Tage-High Filter anwenden
            if (await IsNear30DayHighAsync(symbol, currentPrice, _thirtyDayHighThreshold))
            {
                Log.Debug($"🚫 Kauf von {symbol} blockiert - zu nah am 30-Tage-High");
                return false;
            }

            // ✅ RSI Filter anwenden (30-Minuten-Basis)
            if (await IsRsiOverboughtAsync(symbol))
            {
                Log.Debug($"🚫 Kauf von {symbol} blockiert - RSI-30Min überkauft");
                return false;
            }

            // Prüfe Trading-Strategie
            var candles = await _client.UnifiedApi.ExchangeData.GetKlinesAsync(symbol, KlineInterval.OneMinute);
            if (!candles.Success || !candles.Data.Any())
                return false;

            var volatitiyScanner = new VolatilityAnalyzer();
            var result = volatitiyScanner.HasSufficientVolatility(candles.Data);
            if (!result)
            {
                Log.Information($"🚫 Kauf von {symbol} blockiert - Volatilität zu gering");
                return false;
            }
            var closes = candles.Data.OrderByDescending(c => c.Time).Select(c => (double)c.ClosePrice).ToList();
            var price = closes.FirstOrDefault();

            if (price == 0)
                return false;

            if (!_cooldownManager.CanBuy(symbol))
                return false;

            bool shouldBuy = ShouldBuy; // && _strategy.ShouldBuy(closes, currentPrice));

            if (!shouldBuy)
                return false;

            if (!_instrumentInfos.TryGetValue(symbol, out var minOrderSize))
                return false;

            var finalOrderSize = minOrderSize.MinimumOrderSize * 1.001m;
            var requiredEurAmount = finalOrderSize * currentPrice * 1.02m;
            if (requiredEurAmount <= 15)
            {
                requiredEurAmount *= 10;
                if (requiredEurAmount < 10)
                {
                    requiredEurAmount = 10.02m;
                }
            }

            Log.Information($"=== 🛒 NEUE POSITION VORBEREITUNG ===");
            Log.Information($"Symbol: {symbol}");
            Log.Information($"✅ 30-Tage-High Filter: BESTANDEN");
            Log.Information($"✅ RSI-30Min Filter: BESTANDEN");
            Log.Information($"Benötigtes Investment: {requiredEurAmount:F2} EUR");

            if (!_profitTracker.CanAffordPurchaseStrict((decimal)requiredEurAmount, symbol))
            {
                Log.Information($"❌ Kauf von {symbol} abgelehnt - Budget nicht ausreichend");
                return false;
            }

            if (!_profitTracker.ReserveBudgetForPurchase((decimal)requiredEurAmount, symbol))
            {
                Log.Error($"❌ Budget-Reservierung für {symbol} fehlgeschlagen!");
                return false;
            }

            var buyResponse = await _client.UnifiedApi.Trading.PlaceOrderAsync(
                symbol,
                OrderSide.Buy,
                OrderType.Market,
                tradeMode: TradeMode.Cash,
                quantity: (decimal)requiredEurAmount);

            _cooldownManager.RecordBuy(symbol);

            var orderId = buyResponse.Data?.OrderId;
            Log.Information($"✅ Order platziert - Order ID: {orderId}");

            // Tatsächlichen Preis aus Order Details holen
            var actualPrice = await OrderPriceHelper.GetActualPriceFromOrderAsync(_client, $"{orderId}");

            if (actualPrice > 0)
            {
                Log.Information($"💰 Tatsächlicher Kaufpreis: {actualPrice:F6} EUR (via Order Details)");
            }
            else
            {
                Log.Warning($"⚠️ Konnte tatsächlichen Preis nicht ermitteln für Order {orderId}");
            }

            if (buyResponse.Success)
            {
                Log.Information($"=== ✅ NEUE POSITION ERSTELLT ===");
                Log.Information($"Symbol: {symbol}");
                Log.Information($"Investment: {requiredEurAmount:F2} EUR");

                var volume = (double)(requiredEurAmount / currentPrice);
                var newPosition = new EnhancedTradingPosition
                {
                    Symbol = symbol,
                    High = currentPrice * 1.005m,
                    Processed = DateTime.UtcNow,
                    OrderId = $"{buyResponse.Data?.OrderId}",
                    Volume = volume * 0.999,
                    OriginalVolume = volume * 0.999,
                };

                // Position initialisieren              
                newPosition.InitializePosition(actualPrice, volume, (decimal)requiredEurAmount);

                _positionManager.AddOrUpdatePosition(newPosition);

                var budgetStatus = _profitTracker.GetBudgetStatus();
                Log.Information($"🛒 POSITION: {symbol} | {requiredEurAmount:F2} EUR | Budget: {budgetStatus.AvailableBudget:F2} EUR");
                return true;
            }
            else
            {
                Log.Error($"❌ Kauf-Order für {symbol} fehlgeschlagen: {buyResponse.Error}");
                _profitTracker.ReleaseBudgetFromSale((decimal)requiredEurAmount, 0, $"{symbol}_FAILED");
                return false;
            }
        }

        private async Task CheckAndExecuteSellsAsync()
        {
            var positions = _positionManager.GetPositions();
            var positionsToRemove = new List<TradingPosition>();
            var allPL = 0.0;
            var allInvested = 0.0;

            foreach (var position in positions)
            {
                if (!_blacklistManager.IsSymbolAllowed(position.Symbol))
                {
                    Log.Warning($"⚠️ Position {position.Symbol} blockiert - Symbol geblacklisted");
                    continue;
                }

                if (!_cooldownManager.CanSell(position.Symbol))
                    continue;

                var response = await _client.UnifiedApi.ExchangeData.GetTickerAsync(position.Symbol);
                if (!response.Success || response.Data == null)
                    continue;

                var currentPrice = response.Data.BestBidPrice;
                position.UpdateWithActualPrice((decimal)currentPrice);

                // Verkaufen wenn Ziel erreicht oder Gewinn möglich
                if (position.CanSell((decimal)currentPrice, _positionManager.CalculateGreenRatio()))
                {
                    var sellSuccess = await ExecuteSellOrderAsync(position, (decimal)currentPrice);
                    var eur = (double)position.CalculateUnrealizedPL((decimal)currentPrice).UnrealizedPL;
                    await TransferToSubAccountAsync(
                        (decimal)eur,
                        "JakeJBlues"
                    );
                    positionsToRemove.Add(position);
                }
                allPL += (double)position.UnrealizedPL;
                allInvested += (double)position.TotalInvestedAmount;
            }

            var fee = allInvested * 0.001; // 0.1% Handelsgebühr

            var fileName = "D:\\Lotus\\Domino\\data\\domino\\html\\info.html";
            Log.Information($"📉 Gesamt-PL aller Positionen: {allPL:F2} maximaler Verlust: {-0.08 * allInvested} EUR von investiert {allInvested}");
            File.WriteAllText(fileName, $"<html><body><h1>Gesamt-PL aller Positionen: {allPL:F2} maximaler Verlust: {-0.08 * allInvested} EUR von investiert {allInvested}</h1><Table>");
            foreach (var position in positions.OrderByDescending(p => p.CalculateUnrealizedPL(p.CurrentMarketPrice).UnrealizedPLPercent))
            {
                File.AppendAllText(fileName, $"<tr><td>{position.Symbol}</td><td>{position.TotalInvestedAmount}</td><td>{(position.CurrentMarketPrice - position.OriginalPurchasePrice) * (decimal)position.OriginalVolume}</td><td>{(position.CurrentMarketPrice - position.OriginalPurchasePrice) * (decimal)position.OriginalVolume / position.TotalInvestedAmount * 100:F2} %</td><td>{position.Volume}</td></tr>");
            }
            File.AppendAllText(fileName, "</body></html>");


            //Positionen entfernen
            foreach (var position in positionsToRemove)
            {
                _positionManager.RemovePosition(position);
            }
            if (_positionManager.GetPositions().Count == 0)
            {
                Log.Information("Keine offenen Positionen mehr vorhanden.");
            }
        }

        private async Task<(bool, decimal)> ExecuteSellOrderAsync(TradingPosition position, decimal currentPrice)
        {
            try
            {
                var assets = await _client.UnifiedApi.Account.GetAccountBalanceAsync();
                var assetName = position.Symbol.Split('-')[0];
                var asset = assets.Data?.Details?.FirstOrDefault(d => d.Asset == assetName);

                if (asset == null || asset.Asset == "EUR" || asset.AvailableBalance <= 0)
                    return (false, 0);

                var actualAvailableBalance = asset.AvailableBalance ?? 0;//asset.AvailableBalance.Value;

                var sellResponse = await _client.UnifiedApi.Trading.PlaceOrderAsync(
                    position.Symbol,
                    OrderSide.Sell,
                    OrderType.Market,
                    tradeMode: TradeMode.Cash,
                    quantity: (decimal)(asset.AvailableBalance));

                if (sellResponse.Success)
                {
                    if (currentPrice != -1)
                    {
                        _cooldownManager.RecordSell(position.Symbol);
                    }

                    var currentPriceSell = await OrderPriceHelper.GetActualPriceFromOrderAsync(_client, $"{sellResponse.Data.OrderId}");

                    var originalInvestmentEUR = position.TotalInvestedAmount;
                    var actualSaleValueEUR = actualAvailableBalance;
                    var actualProfit = actualSaleValueEUR - originalInvestmentEUR;

                    _profitTracker.ReleaseBudgetFromSale(originalInvestmentEUR, actualSaleValueEUR, position.Symbol);

                    Log.Information($"=== ✅ VERKAUF ERFOLGREICH ===");
                    Log.Information($"Symbol: {position.Symbol}");
                    Log.Information($"Durchschnittspreis: {position.PurchasePrice:F6} EUR (Original: {position.OriginalPurchasePrice:F6})");
                    Log.Information($"Verkauft bei: {currentPriceSell:F6} EUR");
                    Log.Information($"Gesamtinvestment: {originalInvestmentEUR:F2} EUR");
                    Log.Information($"Verkaufswert: {actualSaleValueEUR:F2} EUR");
                    Log.Information($"Profit/Verlust: {actualProfit:F2} EUR");

                    if (position.AverageDownCount > 0)
                    {
                        Log.Information($"Average-Down Käufe: {position.AverageDownCount}");
                        var improvement = ((position.OriginalPurchasePrice - position.PurchasePrice) / position.OriginalPurchasePrice * 100);
                        Log.Information($"Preis-Verbesserung: {improvement:F2}%");
                    }

                    var budgetStatus = _profitTracker.GetBudgetStatus();
                    Log.Information($"💰 Budget nach Verkauf: {budgetStatus.AvailableBudget:F2} EUR");

                    return (true, currentPriceSell);
                }
                else
                {
                    Log.Error($"❌ Verkaufsfehler {position.Symbol}: {sellResponse.Error}");
                    return (false, 0);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"❌ Fehler beim Verkauf von {position.Symbol}");
                return (false, 0);
            }
        }

        private async Task LogBudgetStatusAsync()
        {
            var budgetStatus = _profitTracker.GetProtectedBudgetStatus();
            var positions = _positionManager.GetPositions();

            // Erwarteten Profit berechnen
            decimal expectedProfit = 0;
            if (positions.Any())
            {
                foreach (var position in positions)
                {
                    try
                    {
                        var ticker = await _client.UnifiedApi.ExchangeData.GetTickerAsync(position.Symbol);
                        if (ticker.Success)
                        {
                            var currentPrice = ticker.Data.LastPrice;
                            var expectedSellValue = position.High * (decimal)position.Volume;
                            expectedProfit += expectedSellValue - position.TotalInvestedAmount;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"Fehler beim Preis-Abruf für {position.Symbol}: {ex.Message}");
                    }
                }
            }

            Log.Information("=== 💰 BUDGET & PROFIT STATUS ===");
            Log.Information($"💰 Verfügbar: {budgetStatus.AvailableTradingBudget:F2} EUR");
            Log.Information($"📊 Investiert: {budgetStatus.TotalInvested:F2} EUR");
            Log.Information($"💎 Realisierter Profit: {budgetStatus.ProtectedProfit:F2} EUR");
            Log.Information($"🎯 Auslastung: {(budgetStatus.TotalInvested / budgetStatus.InitialBudget * 100):F1}%");

            // Filter-Status anzeigen
            if (_enableThirtyDayHighFilter)
            {
                Log.Information($"🛡️ 30-Tage-High Filter: AKTIV (Schwelle: {_thirtyDayHighThreshold * 100:F0}%)");
            }
            else
            {
                Log.Information($"🔓 30-Tage-High Filter: INAKTIV");
            }

            if (_enableRsiFilter)
            {
                Log.Information($"📊 RSI-30Min Filter: AKTIV (Schwelle: {_rsiThreshold}, Periode: {_rsiPeriod})");
            }
            else
            {
                Log.Information($"🔓 RSI-30Min Filter: INAKTIV");
            }

            if (expectedProfit > 0)
            {
                Log.Information($"📈 Erwarteter Profit: {expectedProfit:F2} EUR");
            }
        }

        private void LogCurrentPositionsStatus()
        {
            var positions = _positionManager.GetPositions();
            var activeLockouts = _cooldownManager.GetActiveLockouts();

            Log.Information($"📈 Aktive Positionen: {positions.Count}");

            if (activeLockouts.Any())
            {
                Log.Information($"🚫 Aktive Sperren: {activeLockouts.Count}");
                foreach (var lockout in activeLockouts.Take(3)) // Nur top 3 zeigen
                {
                    Log.Information($"   {lockout.Key}: {lockout.Value.TotalMinutes:F1} Min verbleibend");
                }
            }

            if (positions.Any())
            {
                foreach (var pos in positions.Take(5)) // Nur top 5 zeigen
                {
                    var avgDownInfo = pos.AverageDownCount > 0 ? $" [AvgDown: {pos.AverageDownCount}/3]" : "";
                    Log.Information($"   📊 {pos.Symbol}: Ziel {pos.High:F4}{avgDownInfo}");
                }
            }
        }
        #endregion

        #region Public Configuration Methods
        /// <summary>
        /// Aktiviert oder deaktiviert den 30-Tage-High Filter
        /// </summary>
        public void SetThirtyDayHighFilter(bool enabled)
        {
            var field = typeof(TradingBotEngine).GetField("_enableThirtyDayHighFilter",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(this, enabled);

            Log.Information($"🛡️ 30-Tage-High Filter: {(enabled ? "AKTIVIERT" : "DEAKTIVIERT")}");
        }

        /// <summary>
        /// Setzt den Schwellenwert für den 30-Tage-High Filter
        /// </summary>
        public void SetThirtyDayHighThreshold(decimal threshold)
        {
            if (threshold < 0.5m || threshold > 1.0m)
            {
                Log.Warning($"⚠️ Ungültiger Schwellenwert: {threshold}. Muss zwischen 0.5 und 1.0 liegen.");
                return;
            }

            var field = typeof(TradingBotEngine).GetField("_thirtyDayHighThreshold",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(this, threshold);

            Log.Information($"🎯 30-Tage-High Schwellenwert gesetzt auf: {threshold * 100:F0}%");
        }

        /// <summary>
        /// Aktiviert oder deaktiviert den RSI Filter
        /// </summary>
        public void SetRsiFilter(bool enabled)
        {
            var field = typeof(TradingBotEngine).GetField("_enableRsiFilter",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(this, enabled);

            Log.Information($"📊 RSI-30Min Filter: {(enabled ? "AKTIVIERT" : "DEAKTIVIERT")}");
        }

        /// <summary>
        /// Setzt den RSI Schwellenwert (Standard: 70)
        /// </summary>
        public void SetRsiThreshold(decimal threshold)
        {
            if (threshold < 50m || threshold > 90m)
            {
                Log.Warning($"⚠️ Ungültiger RSI-Schwellenwert: {threshold}. Muss zwischen 50 und 90 liegen.");
                return;
            }

            var field = typeof(TradingBotEngine).GetField("_rsiThreshold",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(this, threshold);

            Log.Information($"📊 RSI-30Min Schwellenwert gesetzt auf: {threshold}");
        }

        /// <summary>
        /// Setzt die RSI Periode (Standard: 14, max. 30 wegen 30-Min Zeitfenster)
        /// </summary>
        public void SetRsiPeriod(int period)
        {
            if (period < 5 || period > 30)
            {
                Log.Warning($"⚠️ Ungültige RSI-Periode: {period}. Muss zwischen 5 und 30 liegen (30-Min Fenster).");
                return;
            }

            var field = typeof(TradingBotEngine).GetField("_rsiPeriod",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(this, period);

            Log.Information($"📊 RSI-30Min Periode gesetzt auf: {period}");
        }

        /// <summary>
        /// Gibt den aktuellen Status des 30-Tage-High Filters zurück
        /// </summary>
        public (bool Enabled, decimal Threshold) GetThirtyDayHighFilterStatus()
        {
            return (_enableThirtyDayHighFilter, _thirtyDayHighThreshold);
        }

        /// <summary>
        /// Gibt den aktuellen Status des RSI Filters zurück
        /// </summary>
        public (bool Enabled, decimal Threshold, int Period) GetRsiFilterStatus()
        {
            return (_enableRsiFilter, _rsiThreshold, _rsiPeriod);
        }
        #endregion
    }
}