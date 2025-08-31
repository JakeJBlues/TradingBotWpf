using CryptoExchange.Net.Authentication;
using Microsoft.Win32;
using Newtonsoft.Json;
using OKX.Net;
using OKX.Net.Clients;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TradingBot;
using TradingBotCore;
using TradingBotCore.Helper;
using TradingBotCore.Manager;

namespace TradingBotWPF
{
    /// <summary>
    /// Enhanced PositionViewModel mit Live-Preis-Updates
    /// </summary>
    public class EnhancedPositionViewModel : INotifyPropertyChanged
    {
        private decimal _currentPrice;
        private decimal _unrealizedPL;
        private decimal _unrealizedPLPercent;
        private DateTime _lastPriceUpdate;
        private decimal _overallPL;
        private bool _isPriceRising;
        private Brush _priceColor = Brushes.Black;

        // Basis-Properties
        public string Symbol { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal OriginalPurchasePrice { get; set; }
        public decimal High { get; set; }
        public double Volume { get; set; }
        public decimal TotalInvestedAmount { get; set; }
        public int AverageDownCount { get; set; }
        public decimal NextAverageDownTrigger { get; set; }
        public bool AverageDownEnabled { get; set; }
        public DateTime LastAverageDownTime { get; set; }

        public decimal OverallPL
        {
            get => _overallPL;
            set
            {
                _overallPL = value;
                OnPropertyChanged(nameof(OverallPL));
            }
        }

        // Live-Preis-Properties
        public decimal CurrentPrice
        {
            get => _currentPrice;
            set
            {
                var oldPrice = _currentPrice;
                _currentPrice = value;
                _isPriceRising = value > oldPrice && oldPrice > 0;
                _lastPriceUpdate = DateTime.UtcNow;

                // Farbe basierend auf Preisentwicklung
                PriceColor = _isPriceRising ? Brushes.Green : (value < oldPrice && oldPrice > 0 ? Brushes.Red : Brushes.Black);
                if (NextAverageDownTrigger == 0)
                {
                    NextAverageDownTrigger = CurrentPrice * 0.99m; // Beispiel-Trigger bei 5% unter Kaufpreis
                }
                // P/L neu berechnen
                UpdateProfitLoss();

                OnPropertyChanged(nameof(CurrentPrice));
                OnPropertyChanged(nameof(CurrentPriceFormatted));
                OnPropertyChanged(nameof(PriceChangeIcon));
                OnPropertyChanged(nameof(LastUpdateFormatted));
                OnPropertyChanged(nameof(CanSellNow));
                OnPropertyChanged(nameof(SellIndicator));
                OnPropertyChanged(nameof(SellIndicatorColor));
                OnPropertyChanged(nameof(NextAverageDownTrigger));
                OnPropertyChanged(nameof(AverageDownCount));
            }
        }

        public decimal UnrealizedPL
        {
            get => _unrealizedPL;
            private set
            {
                _unrealizedPL = value;
                OnPropertyChanged(nameof(UnrealizedPL));
                OnPropertyChanged(nameof(UnrealizedPLFormatted));
                OnPropertyChanged(nameof(ProfitLossColor));
            }
        }

        public decimal UnrealizedPLPercent
        {
            get => _unrealizedPLPercent;
            private set
            {
                _unrealizedPLPercent = value;
                OnPropertyChanged(nameof(UnrealizedPLPercent));
                OnPropertyChanged(nameof(UnrealizedPLPercentFormatted));
            }
        }

        public Brush PriceColor
        {
            get => _priceColor;
            private set
            {
                _priceColor = value;
                OnPropertyChanged(nameof(PriceColor));
            }
        }

        // Formatierte Properties für UI
        public string CurrentPriceFormatted => $"{CurrentPrice:F6} EUR";
        public string UnrealizedPLFormatted => $"{UnrealizedPL:F2} EUR";
        public string UnrealizedPLPercentFormatted => $"{UnrealizedPLPercent:F2}%";
        public string PriceChangeIcon => _isPriceRising ? "📈" : "📉";
        public string LastUpdateFormatted => $"{_lastPriceUpdate:HH:mm:ss}";

        public Brush ProfitLossColor => UnrealizedPL >= 0 ? Brushes.Green : Brushes.Red;

        // Verkaufs-Indikator
        public bool CanSellNow => CurrentPrice > High || CurrentPrice > PurchasePrice * 1.005m;
        public string SellIndicator => CanSellNow ? "🎯 VERKAUFEN" : "⏳ Halten";
        public Brush SellIndicatorColor => CanSellNow ? Brushes.Orange : Brushes.Gray;

        private void UpdateProfitLoss()
        {
            if (Volume > 0 && CurrentPrice > 0)
            {
                var currentValue = CurrentPrice * (decimal)Volume;
                UnrealizedPL = currentValue - TotalInvestedAmount;
                UnrealizedPLPercent = TotalInvestedAmount > 0 ? (UnrealizedPL / TotalInvestedAmount) * 100 : 0;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Live-Preis-Update-Service
    /// </summary>
    public class LivePriceUpdateService
    {
        private readonly OKXRestClient _client;
        private readonly DispatcherTimer _updateTimer;
        private readonly ObservableCollection<EnhancedPositionViewModel> _positions;
        private readonly Action<string> _logAction;
        private bool _isUpdating = false;
        private DateTime _lastGlobalUpdate = DateTime.MinValue;

        public LivePriceUpdateService(OKXRestClient client, ObservableCollection<EnhancedPositionViewModel> positions, Action<string> logAction)
        {
            _client = client;
            _positions = positions;
            _logAction = logAction;

            // Update alle 5 Sekunden
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
        }

        public void StartUpdates()
        {
            _updateTimer.Start();
            _logAction?.Invoke("📈 Live-Preis-Updates gestartet");
        }

        public void StopUpdates()
        {
            _updateTimer.Stop();
            _logAction?.Invoke("📉 Live-Preis-Updates gestoppt");
        }

        public bool IsRunning => _updateTimer.IsEnabled;
        public DateTime LastUpdate => _lastGlobalUpdate;

        private async void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (_isUpdating) return;

            try
            {
                _isUpdating = true;
                await UpdateAllPricesAsync();
                _lastGlobalUpdate = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"❌ Fehler beim Preis-Update: {ex.Message}");
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private async Task UpdateAllPricesAsync()
        {
            var positions = _positions.ToList(); // Thread-safe copy
            if (!positions.Any()) return;

            var updateTasks = positions.Select(UpdatePositionPriceAsync);
            await Task.WhenAll(updateTasks);
        }

        private async Task UpdatePositionPriceAsync(EnhancedPositionViewModel position)
        {
            try
            {
                var response = await _client.UnifiedApi.ExchangeData.GetTickerAsync(position.Symbol);
                if (response.Success && response.Data != null)
                {
                    var newPrice = (decimal)response.Data.BestBidPrice;

                    // Thread-safe UI-Update
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        position.CurrentPrice = newPrice;
                    });
                }
                else
                {
                    await Task.Delay(Login.NoSuccessDelay);
                }
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"⚠️ Preis-Update für {position.Symbol} fehlgeschlagen: {ex.Message}");
            }
        }

        public void SetUpdateInterval(TimeSpan interval)
        {
            _updateTimer.Interval = interval;
            _logAction?.Invoke($"🔄 Update-Intervall geändert auf {interval.TotalSeconds}s");
        }
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Private Fields
        private bool _isBotRunning = false;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly DispatcherTimer _updateTimer;
        private readonly EnhancedTradingCooldownManager _cooldownManager;
        private readonly PositionManager _positionManager;
        private readonly ProtectedProfitTracker _profitTracker;
        private readonly TradingBlacklistManager _blacklistManager;

        private TradingBotEngine _botEngine;

        // Enhanced Collections für Live-Updates
        private readonly ObservableCollection<EnhancedPositionViewModel> _enhancedPositions = new();
        private readonly ObservableCollection<EnhancedCooldownViewModel> _cooldowns = new();
        private readonly ObservableCollection<BlacklistItemViewModel> _blacklistItems = new();

        // Live-Price-Service
        private LivePriceUpdateService _priceUpdateService;
        private OKXRestClient _client;
        #endregion

        #region Properties für Data Binding
        public decimal AvailableBudget { get; set; }
        public decimal InvestedAmount { get; set; }
        public decimal RealizedProfit { get; set; }
        public decimal InitialBalance { get; set; }
        public decimal OverAllPL { get; set; }
        public double BudgetUtilization { get; set; }
        public int PositionsCount { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize Trading Components mit Enhanced Manager
            _cooldownManager = new EnhancedTradingCooldownManager(
                TimeSpan.FromMinutes(10),
                TimeSpan.FromSeconds(1),  // Geändert von 0!
                TimeSpan.FromMilliseconds(30), // Geändert von 1 Mikrosekunde!
                TimeSpan.FromMinutes(5)
            );

            _positionManager = new PositionManager();
            _profitTracker = new ProtectedProfitTracker();
            _blacklistManager = new TradingBlacklistManager();

            // Initialize Bot Engine
            _botEngine = new TradingBotEngine(_cooldownManager, _positionManager, _profitTracker, _blacklistManager);

            // Setup Update Timer
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _updateTimer.Tick += UpdateTimer_Tick;

            // Setup Enhanced UI Collections
            PositionsListView.ItemsSource = _enhancedPositions;
            DetailedPositionsGrid.ItemsSource = _enhancedPositions;
            CooldownsListView.ItemsSource = _cooldowns;
            BlacklistItemsListView.ItemsSource = _blacklistItems;

            Title = $"TradingBot WPF {Login.FilePreSuffix} - Version {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";
            // Setup Logging
            SetupLogging();

            // Initialize UI
            LoadSettings();

            LogToUI("🚀 TradingBot WPF Application mit Live-Preis-Updates gestartet");
        }
        #endregion

        #region Logging Setup
        private void SetupLogging()
        {
            var logToUI = new CustomUISink(LogToUI);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs/tradingbot-wpf.log", rollingInterval: RollingInterval.Day)
                .WriteTo.Sink(logToUI)
                .CreateLogger();
        }

        private void LogToUI(string message)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (LogTextBox.LineCount > 150)
                {
                    LogTextBox.Clear();
                    LogTextBox.AppendText("🗑️ Log zu groß, wurde gelöscht\n");
                }
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                LogTextBox.AppendText($"[{timestamp}] {message}\n");

                if (AutoScrollCheckBox.IsChecked == true)
                {
                    LogScrollViewer.ScrollToEnd();
                }
            });
        }
        #endregion

        #region Bot Control Events
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isBotRunning) return;

            try
            {
                StartButton.IsEnabled = false;
                LogToUI("🤖 Starte TradingBot...");

                _cancellationTokenSource = new CancellationTokenSource();

                if (!ValidateSettings())
                {
                    LogToUI("❌ Ungültige Einstellungen - Bot-Start abgebrochen");
                    StartButton.IsEnabled = true;
                    return;
                }

                // OKX Client initialisieren
                _client = new OKXRestClient(options =>
                {
                    options.ApiCredentials = new ApiCredentials("c77c9afa-fed2-4eba-915c-f8d4eb23aba2", "63686C8CB72F797FB94CB63E5E1A6776", "oGlennyweg2311!x");
                    options.Environment = OKXEnvironment.Europe;
                });

                // Live-Price-Service starten
                _priceUpdateService = new LivePriceUpdateService(_client, _enhancedPositions, LogToUI);
                _priceUpdateService.StartUpdates();

                await _botEngine.ConfigureAsync(GetBotConfiguration());
                var botTask = _botEngine.StartAsync(_cancellationTokenSource.Token);

                _isBotRunning = true;
                UpdateBotStatus();
                _updateTimer.Start();

                LogToUI("✅ TradingBot erfolgreich gestartet!");
                LogToUI("📈 Live-Preis-Updates aktiviert");

                await botTask;
            }
            catch (Exception ex)
            {
                LogToUI($"❌ Fehler beim Starten des Bots: {ex.Message}");
                _isBotRunning = false;
                UpdateBotStatus();
            }
            finally
            {
                StartButton.IsEnabled = true;
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isBotRunning) return;

            try
            {
                StopButton.IsEnabled = false;
                LogToUI("🛑 Stoppe TradingBot...");

                // Live-Price-Service stoppen
                _priceUpdateService?.StopUpdates();

                _cancellationTokenSource?.Cancel();
                _botEngine.StopAsync();
                _botEngine.SellAllPositions();

                _isBotRunning = false;
                _updateTimer.Stop();
                UpdateBotStatus();

                LogToUI("✅ TradingBot erfolgreich gestoppt!");
                LogToUI("📉 Live-Preis-Updates deaktiviert");
            }
            catch (Exception ex)
            {
                LogToUI($"❌ Fehler beim Stoppen des Bots: {ex.Message}");
            }
            finally
            {
                StopButton.IsEnabled = true;
            }
        }

        private void UpdateBotStatus()
        {
            if (_isBotRunning)
            {
                StatusIndicator.Text = "🟢 LÄUFT";
                StatusIndicator.Foreground = new SolidColorBrush(Colors.Green);
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;

                // Live-Updates Status
                PriceUpdateStatusText.Text = "Live-Updates: Aktiv";
                PriceUpdateStatusText.Foreground = Brushes.Green;
            }
            else
            {
                StatusIndicator.Text = "⚫ GESTOPPT";
                StatusIndicator.Foreground = new SolidColorBrush(Colors.Red);
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;

                // Live-Updates Status
                PriceUpdateStatusText.Text = "Live-Updates: Inaktiv";
                PriceUpdateStatusText.Foreground = Brushes.Red;
            }
        }
        #endregion

        #region Enhanced Data Update Methods
        private async void UpdateTimer_Tick(object sender, EventArgs e)
        {
            await RefreshAllData();

            // Update Live-Price-Status
            if (_priceUpdateService != null)
            {
                LastGlobalUpdateText.Text = $"(Letztes Update: {_priceUpdateService.LastUpdate:HH:mm:ss})";
            }
        }

        private async Task RefreshAllData()
        {
            try
            {
                await RefreshBudgetStatus();
                await RefreshEnhancedPositions();
                await RefreshCooldowns();
                await RefreshBlacklist();
            }
            catch (Exception ex)
            {
                LogToUI($"❌ Fehler beim Aktualisieren der Daten: {ex.Message}");
            }
        }

        // Enhanced Positions mit Live-Preis-Integration
        private async Task RefreshEnhancedPositions()
        {
            var positions = _positionManager.GetPositions();
            PositionsCount = positions.Count;
            PositionsCountText.Text = $"Anzahl Positionen: {PositionsCount}";

            // Bestehende Enhanced Positions aktualisieren oder neue hinzufügen
            var currentSymbols = _enhancedPositions.Select(p => p.Symbol).ToHashSet();
            var newSymbols = positions.Select(p => p.Symbol).ToHashSet();

            // Entferne nicht mehr existierende Positionen
            var toRemove = _enhancedPositions.Where(ep => !newSymbols.Contains(ep.Symbol)).ToList();
            foreach (var item in toRemove)
            {
                _enhancedPositions.Remove(item);
            }

            // Füge neue Positionen hinzu oder aktualisiere bestehende
            foreach (var position in positions)
            {
                var existing = _enhancedPositions.FirstOrDefault(ep => ep.Symbol == position.Symbol);
                if (existing == null)
                {
                    // Neue Position hinzufügen
                    var enhanced = new EnhancedPositionViewModel
                    {
                        Symbol = position.Symbol,
                        PurchasePrice = position.PurchasePrice,
                        OriginalPurchasePrice = position.OriginalPurchasePrice,
                        High = position.High,
                        Volume = position.Volume,
                        TotalInvestedAmount = position.TotalInvestedAmount,
                        AverageDownCount = position.AverageDownCount,
                        NextAverageDownTrigger = position.NextAverageDownTrigger,
                        AverageDownEnabled = position.AverageDownEnabled,
                        LastAverageDownTime = position.LastAverageDownTime
                    };

                    // Initialen Preis setzen (wird dann von Live-Service aktualisiert)
                    var currentPrice = await GetCurrentPriceAsync(position.Symbol);
                    enhanced.CurrentPrice = currentPrice;

                    _enhancedPositions.Add(enhanced);
                }
                else
                {
                    // Bestehende Position aktualisieren (ohne Live-Preis zu überschreiben)
                    existing.PurchasePrice = position.PurchasePrice;
                    existing.Volume = position.Volume;
                    existing.TotalInvestedAmount = position.TotalInvestedAmount;
                    existing.AverageDownCount = position.AverageDownCount;
                    existing.NextAverageDownTrigger = position.NextAverageDownTrigger;
                    existing.AverageDownEnabled = position.AverageDownEnabled;
                }
            }

            OnPropertyChanged(nameof(PositionsCount));
        }

        private async Task RefreshBudgetStatus()
        {
            var budgetStatus = _profitTracker.GetProtectedBudgetStatus();

            AvailableBudget = budgetStatus.AvailableTradingBudget;
            InvestedAmount = budgetStatus.TotalInvested;
            RealizedProfit = budgetStatus.ProtectedProfit;
            InitialBalance = budgetStatus.InitialBudget;
            OverAllPL = budgetStatus.OverallPL;

            BudgetUtilization = InitialBalance > 0 ? (double)(InvestedAmount / InitialBalance * 100) : 0;

            // Update UI
            AvailableBudgetText.Text = $"{AvailableBudget:F2} EUR";
            InvestedAmountText.Text = $"{InvestedAmount:F2} EUR";
            RealizedProfitText.Text = $"{RealizedProfit:F2} EUR";
            RealizedProfitText.Foreground = RealizedProfit >= 0 ? Brushes.Green : Brushes.Red;
            InitialBalanceText.Text = $"{InitialBalance:F2} EUR";
            BudgetUtilizationBar.Value = BudgetUtilization;
            OverAllPLText.Text = $"{OverAllPL:F2} EUR";

            OnPropertyChanged(nameof(AvailableBudget));
            OnPropertyChanged(nameof(InvestedAmount));
            OnPropertyChanged(nameof(RealizedProfit));
            OnPropertyChanged(nameof(InitialBalance));
            OnPropertyChanged(nameof(BudgetUtilization));
            OnPropertyChanged(nameof(OverAllPL));
        }

        // Erweiterte Cooldown-Anzeige mit ALLEN Cooldown-Typen
        private async Task RefreshCooldowns()
        {
            var allCooldowns = _cooldownManager.GetAllActiveCooldowns();

            _cooldowns.Clear();
            foreach (var cooldown in allCooldowns)
            {
                _cooldowns.Add(new EnhancedCooldownViewModel
                {
                    Symbol = cooldown.Symbol,
                    RemainingTime = cooldown.Type == CooldownType.GlobalCooldown
                        ? $"{cooldown.RemainingTime.TotalSeconds:F0}s"
                        : $"{cooldown.RemainingTime.TotalMinutes:F1} Min",
                    Type = GetCooldownTypeDisplayName(cooldown.Type),
                    Description = cooldown.Description,
                    ExpiresAt = cooldown.ExpiresAt,
                    CooldownType = cooldown.Type
                });
            }

            // Cooldown-Statistiken loggen
            var stats = _cooldownManager.GetCooldownStatistics();
            if (stats.TotalActiveCooldowns > 0)
            {
                Log.Debug($"📊 Cooldown-Status: {stats.Summary}");
            }
        }

        private string GetCooldownTypeDisplayName(CooldownType type)
        {
            return type switch
            {
                CooldownType.BuyCooldown => "Kauf-Cooldown",
                CooldownType.SellCooldown => "Verkauf-Cooldown",
                CooldownType.SellLockout => "Verkaufssperre",
                CooldownType.GlobalCooldown => "Globaler Cooldown",
                _ => "Unbekannt"
            };
        }

        private async Task RefreshBlacklist()
        {
            var status = _blacklistManager.GetBlacklistStatus();

            _blacklistItems.Clear();

            foreach (var asset in status.Assets)
            {
                _blacklistItems.Add(new BlacklistItemViewModel
                {
                    Item = asset,
                    Type = "Asset"
                });
            }

            foreach (var symbol in status.Symbols)
            {
                _blacklistItems.Add(new BlacklistItemViewModel
                {
                    Item = symbol,
                    Type = "Symbol"
                });
            }
        }

        // Echte API-Implementierung statt Mock
        private async Task<decimal> GetCurrentPriceAsync(string symbol)
        {
            try
            {
                if (_client != null)
                {
                    var response = await _client.UnifiedApi.ExchangeData.GetTickerAsync(symbol);
                    return response.Success ? (decimal)response.Data.LastPrice : 0m;
                }
                return 50000m; // Fallback
            }
            catch (Exception ex)
            {
                LogToUI($"⚠️ Preis-Abruf für {symbol} fehlgeschlagen: {ex.Message}");
                return 0m;
            }
        }
        #endregion

        #region UI Event Handlers (keeping existing ones)
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LogToUI("🔄 Aktualisiere Daten...");
            await RefreshAllData();
            LogToUI("✅ Daten aktualisiert");
        }

        // ... (alle anderen Event Handler bleiben gleich) ...

        private void ShowAverageDownStatus_Click(object sender, RoutedEventArgs e)
        {
            var positions = _positionManager.GetPositions();
            var avgDownPositions = positions.Where(p => p.AverageDownCount > 0 || p.AverageDownEnabled).ToList();

            LogToUI("=== 🔄 AVERAGE-DOWN STATUS ===");

            if (!avgDownPositions.Any())
            {
                LogToUI("Keine Positionen mit Average-Down aktiviert");
                return;
            }

            foreach (var pos in avgDownPositions)
            {
                var status = pos.AverageDownEnabled ? "AKTIV" : "DEAKTIVIERT";
                var icon = pos.AverageDownEnabled ? "🟢" : "🔴";

                LogToUI($"{icon} {pos.Symbol}: {status} | Käufe: {pos.AverageDownCount}/3 | Trigger: {pos.NextAverageDownTrigger:F6} EUR");

                if (pos.AverageDownHistory.Any())
                {
                    var history = string.Join(", ", pos.AverageDownHistory.Select(h => $"{h.Price:F4}@{h.Timestamp:HH:mm}"));
                    LogToUI($"   Historie: {history}");
                }
            }
        }

        private void ShowAverageDownStats_Click(object sender, RoutedEventArgs e)
        {
            var positions = _positionManager.GetPositions();
            var avgDownPositions = positions.Where(p => p.AverageDownCount > 0).ToList();

            if (!avgDownPositions.Any())
            {
                LogToUI("=== 📈 AVERAGE-DOWN STATISTIKEN ===");
                LogToUI("Keine Positionen mit Average-Down Käufen vorhanden");
                return;
            }

            var totalAvgDowns = avgDownPositions.Sum(p => p.AverageDownCount);
            var avgPriceImprovement = avgDownPositions.Average(p =>
                ((p.OriginalPurchasePrice - p.PurchasePrice) / p.OriginalPurchasePrice * 100));

            LogToUI("=== 📈 AVERAGE-DOWN STATISTIKEN ===");
            LogToUI($"Positionen mit Average-Down: {avgDownPositions.Count}");
            LogToUI($"Gesamt Average-Down Käufe: {totalAvgDowns}");
            LogToUI($"Durchschnittliche Preis-Verbesserung: {avgPriceImprovement:F2}%");

            LogToUI("--- Details pro Position ---");
            foreach (var pos in avgDownPositions)
            {
                var improvement = ((pos.OriginalPurchasePrice - pos.PurchasePrice) / pos.OriginalPurchasePrice * 100);
                LogToUI($"{pos.Symbol}: {pos.AverageDownCount} Käufe, {improvement:F2}% Verbesserung");
            }
        }

        private void DisableAllAverageDowns_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Möchten Sie wirklich Average-Down für ALLE Positionen deaktivieren?",
                "Bestätigung",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var positions = _positionManager.GetPositions();
                var count = 0;

                foreach (var pos in positions.Where(p => p.AverageDownEnabled))
                {
                    pos.DisableAverageDown("Global über UI deaktiviert");
                    count++;
                }

                LogToUI($"❌ Average-Down für {count} Positionen deaktiviert");
                RefreshEnhancedPositions();
            }
        }

        private void DisableAverageDownForSymbol_Click(object sender, RoutedEventArgs e)
        {
            var symbol = SymbolTextBox.Text.Trim();
            if (string.IsNullOrEmpty(symbol))
            {
                MessageBox.Show("Bitte geben Sie ein Symbol ein.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var position = _positionManager.GetPositionBySymbol(symbol);
            if (position != null)
            {
                position.DisableAverageDown($"Über UI deaktiviert für {symbol}");
                LogToUI($"❌ Average-Down für {symbol} deaktiviert");
                RefreshEnhancedPositions();
            }
            else
            {
                LogToUI($"⚠️ Keine Position für {symbol} gefunden");
            }
        }

        private void RefreshCooldowns_Click(object sender, RoutedEventArgs e)
        {
            LogToUI("🔄 Aktualisiere Cooldowns...");
            RefreshCooldowns();
            LogToUI("✅ Cooldowns aktualisiert");
        }

        private void ShowBlacklistStatus_Click(object sender, RoutedEventArgs e)
        {
            _blacklistManager.LogBlacklistStatus();
            RefreshBlacklist();
        }

        private void AddToBlacklist_Click(object sender, RoutedEventArgs e)
        {
            var item = BlacklistItemTextBox.Text.Trim();
            var typeItem = BlacklistTypeComboBox.SelectedItem as ComboBoxItem;

            if (string.IsNullOrEmpty(item) || typeItem == null)
            {
                MessageBox.Show("Bitte füllen Sie alle Felder aus.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var type = typeItem.Content.ToString();

            if (type == "Asset")
            {
                _blacklistManager.AddAssetToBlacklist(item);
            }
            else if (type == "Symbol")
            {
                _blacklistManager.AddSymbolToBlacklist(item);
            }

            BlacklistItemTextBox.Text = "";
            RefreshBlacklist();
            LogToUI($"➕ {type} '{item}' zur Blacklist hinzugefügt");
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = new TradingBotSettings
                {
                    ApiKey = ApiKeyTextBox.Text,
                    Secret = SecretPasswordBox.Password,
                    Passphrase = PassphrasePasswordBox.Password,
                    BuyCooldownMinutes = int.Parse(BuyCooldownTextBox.Text),
                    SellCooldownMinutes = int.Parse(SellCooldownTextBox.Text),
                    GlobalCooldownSeconds = int.Parse(GlobalCooldownTextBox.Text),
                    SellLockoutMinutes = int.Parse(SellLockoutTextBox.Text),
                    AverageDownEnabled = AverageDownEnabledCheckBox.IsChecked ?? true
                };

                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText("settings.json", json);

                LogToUI("💾 Einstellungen gespeichert");
                MessageBox.Show("Einstellungen erfolgreich gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogToUI($"❌ Fehler beim Speichern: {ex.Message}");
                MessageBox.Show($"Fehler beim Speichern der Einstellungen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Text = "";
            LogToUI("🗑️ Log gelöscht");
        }

        private void SaveLog_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"tradingbot-log-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.txt"
            };

            if (saveDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveDialog.FileName, LogTextBox.Text);
                LogToUI($"💾 Log gespeichert: {saveDialog.FileName}");
            }
        }
        #endregion

        #region Helper Methods
        private bool ValidateSettings()
        {
            if (string.IsNullOrWhiteSpace(ApiKeyTextBox.Text))
            {
                MessageBox.Show("API Key ist erforderlich.", "Validierungsfehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(SecretPasswordBox.Password))
            {
                MessageBox.Show("Secret ist erforderlich.", "Validierungsfehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(PassphrasePasswordBox.Password))
            {
                MessageBox.Show("Passphrase ist erforderlich.", "Validierungsfehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private TradingBotConfiguration GetBotConfiguration()
        {
            return new TradingBotConfiguration
            {
                ApiKey = ApiKeyTextBox.Text,
                Secret = SecretPasswordBox.Password,
                Passphrase = PassphrasePasswordBox.Password,
                BuyCooldown = TimeSpan.FromMinutes(int.Parse(BuyCooldownTextBox.Text)),
                SellCooldown = TimeSpan.FromMinutes(int.Parse(SellCooldownTextBox.Text)),
                GlobalCooldown = TimeSpan.FromSeconds(int.Parse(GlobalCooldownTextBox.Text)),
                SellLockout = TimeSpan.FromMinutes(int.Parse(SellLockoutTextBox.Text)),
                AverageDownEnabled = AverageDownEnabledCheckBox.IsChecked ?? true
            };
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists("settings.json"))
                {
                    var json = File.ReadAllText("settings.json");
                    var settings = JsonConvert.DeserializeObject<TradingBotSettings>(json);

                    ApiKeyTextBox.Text = settings.ApiKey ?? "";
                    SecretPasswordBox.Password = settings.Secret ?? "";
                    PassphrasePasswordBox.Password = settings.Passphrase ?? "";
                    BuyCooldownTextBox.Text = settings.BuyCooldownMinutes.ToString();
                    SellCooldownTextBox.Text = settings.SellCooldownMinutes.ToString();
                    GlobalCooldownTextBox.Text = settings.GlobalCooldownSeconds.ToString();
                    SellLockoutTextBox.Text = settings.SellLockoutMinutes.ToString();
                    AverageDownEnabledCheckBox.IsChecked = settings.AverageDownEnabled;

                    LogToUI("📁 Einstellungen geladen");
                }
                else
                {
                    LogToUI("⚠️ Keine gespeicherten Einstellungen gefunden - verwende Standardwerte");
                }
            }
            catch (Exception ex)
            {
                LogToUI($"❌ Fehler beim Laden der Einstellungen: {ex.Message}");
            }
        }
        #endregion

        #region Window Events
        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isBotRunning)
            {
                var result = MessageBox.Show(
                    "Der Bot läuft noch. Möchten Sie ihn stoppen und die Anwendung beenden?",
                    "Bot läuft",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _cancellationTokenSource?.Cancel();
                    _updateTimer?.Stop();
                    _priceUpdateService?.StopUpdates();
                }
                else
                {
                    e.Cancel = true;
                    return;
                }
            }

            base.OnClosing(e);
        }
        #endregion
    }

    #region Supporting Classes
    public class EnhancedCooldownViewModel
    {
        public string Symbol { get; set; }
        public string RemainingTime { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public DateTime ExpiresAt { get; set; }
        public CooldownType CooldownType { get; set; }

        // UI-Properties
        public string TypeIcon => CooldownType switch
        {
            CooldownType.BuyCooldown => "🛒",
            CooldownType.SellCooldown => "💰",
            CooldownType.SellLockout => "🚫",
            CooldownType.GlobalCooldown => "⏰",
            _ => "❓"
        };

        public string ExpiresAtFormatted => ExpiresAt.ToString("HH:mm:ss");

        public string Priority => CooldownType switch
        {
            CooldownType.GlobalCooldown => "HOCH",
            CooldownType.SellLockout => "MITTEL",
            CooldownType.BuyCooldown => "NIEDRIG",
            CooldownType.SellCooldown => "NIEDRIG",
            _ => "UNBEKANNT"
        };
    }

    public class BlacklistItemViewModel
    {
        public string Item { get; set; }
        public string Type { get; set; }
    }

    public class TradingBotSettings
    {
        public string ApiKey { get; set; }
        public string Secret { get; set; }
        public string Passphrase { get; set; }
        public int BuyCooldownMinutes { get; set; } = 10;
        public int SellCooldownMinutes { get; set; } = 2; // Geändert von 0!
        public int GlobalCooldownSeconds { get; set; } = 30; // Geändert von 1!
        public int SellLockoutMinutes { get; set; } = 5;
        public bool AverageDownEnabled { get; set; } = true;
    }

    public class TradingBotConfiguration
    {
        public string ApiKey { get; set; }
        public string Secret { get; set; }
        public string Passphrase { get; set; }
        public TimeSpan BuyCooldown { get; set; }
        public TimeSpan SellCooldown { get; set; }
        public TimeSpan GlobalCooldown { get; set; }
        public TimeSpan SellLockout { get; set; }
        public bool AverageDownEnabled { get; set; } = true;
        public decimal BaseInvestmentAmount { get; internal set; } = 10.0m;
    }

    public class CustomUISink : Serilog.Core.ILogEventSink
    {
        private readonly Action<string> _writeAction;

        public CustomUISink(Action<string> writeAction)
        {
            _writeAction = writeAction;
        }

        public void Emit(Serilog.Events.LogEvent logEvent)
        {
            var message = logEvent.RenderMessage();
            _writeAction?.Invoke(message);
        }
    }
    #endregion
}