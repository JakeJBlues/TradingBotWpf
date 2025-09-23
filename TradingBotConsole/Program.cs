using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using TradingBot;
using TradingBotCore;
using TradingBotCore.Entitites;
using TradingBotCore.Helper;
using TradingBotCore.Manager;

namespace MinimalTradingLoop
{
    /// <summary>
    /// Minimale Trading-Schleife mit TradingBotEngine - nur das Nötigste
    /// </summary>
    class Program
    {
        private static TradingBotEngine _botEngine;
        private static CancellationTokenSource _cancellationTokenSource;

        static async Task Main(string[] args)
        {
            // Serilog Konsolen-Logging Setup
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information("🤖 Minimal Trading Loop gestartet");
                Log.Information($"💰 Budget: {Login.MaximalTradingBudget} EUR | Min Position: {Login.MinimalTradingPostionSize} EUR");

                // Bot Setup
                await InitializeBotAsync();

                // Abbruch mit Ctrl+C
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    Log.Information("🛑 Stopp-Signal empfangen - beende Trading Loop...");
                    _cancellationTokenSource?.Cancel();
                };

                Log.Information("🔄 Trading Loop läuft... (Ctrl+C zum Stoppen)");

                // Trading Loop starten
                _cancellationTokenSource = new CancellationTokenSource();
                await _botEngine.StartAsync(_cancellationTokenSource.Token);

            }
            catch (OperationCanceledException)
            {
                Log.Information("🛑 Trading Loop gestoppt");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "💥 Kritischer Fehler");
                Console.WriteLine("\nDrücken Sie eine Taste zum Beenden...");
                Console.ReadKey();
            }
            finally
            {
                Log.Information("👋 Programm beendet");
                Log.CloseAndFlush();
            }
        }

        private static async Task InitializeBotAsync()
        {
            Log.Information("⚙️ Initialisiere Trading Bot Components...");

            // Manager mit Standard-Einstellungen
            var cooldownManager = new TradingCooldownManager(
                buyDelay: TimeSpan.FromMinutes(10),
                sellDelay: TimeSpan.FromMinutes(1),
                globalCooldown: TimeSpan.FromSeconds(30),
                sellLockoutDuration: TimeSpan.FromMinutes(5)
            );

            var positionManager = new PositionManager();
            var profitTracker = new ProtectedProfitTracker();
            var blacklistManager = new TradingBlacklistManager();

            // TradingBotEngine erstellen
            _botEngine = new TradingBotEngine(
                cooldownManager,
                positionManager,
                profitTracker,
                blacklistManager
            );

            // Minimal-Konfiguration
            var configuration = new TradingBotConfiguration
            {
                ApiKey = "FROM_LOGIN",
                Secret = "FROM_LOGIN",
                Passphrase = "FROM_LOGIN",
                BuyCooldown = TimeSpan.FromMinutes(10),
                SellCooldown = TimeSpan.FromMinutes(1),
                GlobalCooldown = TimeSpan.FromSeconds(30),
                SellLockout = TimeSpan.FromMinutes(5),
                AverageDownEnabled = Login.AverageDownEnabled,
                BaseInvestmentAmount = Login.MinimalTradingPostionSize
            };

            // Engine konfigurieren
            await _botEngine.ConfigureAsync(configuration);

            Log.Information("✅ Trading Bot bereit");
        }
    }
}