using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using TradingBot;
using TradingBotCore;
using TradingBotCore.Entitites;
using TradingBotCore.Helper;
using TradingBotCore.Manager;

namespace TradingBotConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Logging konfigurieren
            ConfigureLogging();

            try
            {
                // FilePreSuffix aus Kommandozeilenargument holen
                Login.FilePreSuffix = GetFilePreSuffixFromArgs(args);

                Log.Information("=== TradingBot Console gestartet ===");
                Log.Information($"FilePreSuffix: {Login.FilePreSuffix}");
                Log.Information($"Account: {Login.GetActualAccount()}");

                Console.ReadLine();
                // Client aus Login-Klasse holen mit dem übergebenen Prefix
                var client = Login.Credentials(Login.FilePreSuffix);

                Log.Information($"✅ OKX Client für Account '{Login.GetActualAccount()}' erstellt");

                // TradingBot Komponenten initialisieren
                var cooldownManager = CreateCooldownManager();
                var positionManager = new PositionManager();
                var profitTracker = new ProtectedProfitTracker();
                var blacklistManager = new TradingBlacklistManager();

                // TradingBotEngine erstellen
                var engine = new TradingBotEngine(
                    cooldownManager,
                    positionManager,
                    profitTracker,
                    blacklistManager
                );

                // Konfiguration erstellen (diese wird intern vom Engine verwendet, 
                // aber der Client kommt direkt aus der Login-Klasse)
                var configuration = new TradingBotConfiguration
                {
                    BuyCooldown = TimeSpan.FromMinutes(1),
                    SellCooldown = TimeSpan.FromMinutes(1),
                    GlobalCooldown = TimeSpan.FromSeconds(30),
                    SellLockout = TimeSpan.FromMinutes(5),
                    AverageDownEnabled = Login.AverageDownEnabled,
                    BaseInvestmentAmount = Login.MinimalTradingPostionSize
                };

                // Engine konfigurieren
                await engine.ConfigureAsync(configuration);

                // CancellationToken für sauberes Beenden
                using var cts = new CancellationTokenSource();

                // CTRL+C Handler
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    Log.Information("🛑 Shutdown Signal empfangen...");
                    cts.Cancel();
                };

                Log.Information("🚀 Starte Trading Loop...");
                Log.Information("Drücke CTRL+C zum Beenden");

                // Trading Loop starten
                await engine.StartAsync(cts.Token);

                Log.Information("✅ TradingBot beendet");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "❌ Kritischer Fehler in TradingBot Console");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// Holt FilePreSuffix aus Kommandozeilenargumenten
        /// </summary>
        private static string GetFilePreSuffixFromArgs(string[] args)
        {
            if (args.Length == 0)
            {
                Log.Warning("⚠️ Kein FilePreSuffix angegeben. Verwende Standard: 'UG2'");
                Log.Information("Usage: TradingBotConsole.exe <FilePreSuffix>");
                Log.Information("Beispiel: TradingBotConsole.exe UG2");
                Log.Information("Verfügbare Prefixes: \"\", \"1\", \"2\", \"3\", \"UG\", \"UG1\", \"UG2\", \"UG3\"");
                return "UG2"; // Standard-Wert
            }

            string prefix = args[0];

            // Validierung des Prefix
            var validPrefixes = new[] { "", "1", "2", "3", "UG", "UG1", "UG2", "UG3" };
            if (!Array.Exists(validPrefixes, p => p == prefix))
            {
                Log.Warning($"⚠️ Ungültiger FilePreSuffix: '{prefix}'. Verwende Standard: 'UG2'");
                Log.Information($"Verfügbare Prefixes: {string.Join(", ", validPrefixes)}");
                return "UG2";
            }

            return prefix;
        }

        /// <summary>
        /// Erstellt CooldownManager mit Standardwerten
        /// </summary>
        private static TradingCooldownManager CreateCooldownManager()
        {
            return new TradingCooldownManager(
                buyDelay: TimeSpan.FromMinutes(1),
                sellDelay: TimeSpan.FromMinutes(1),
                globalCooldown: TimeSpan.FromSeconds(30),
                sellLockoutDuration: TimeSpan.FromMinutes(5)
            );
        }

        /// <summary>
        /// Konfiguriert Serilog für Console und File Logging
        /// </summary>
        private static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: $"logs/tradingbot-{Login.FilePreSuffix}-.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    retainedFileCountLimit: 30)
                .CreateLogger();
        }
    }
}