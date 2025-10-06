using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using TradingBot;
using TradingBotCore;
using TradingBotCore.Entitites;
using TradingBotCore.Helper;
using TradingBotCore.Manager;
using OKX.Net.Enums;

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
                // Parse alle Kommandozeilenargumente
                ParseArguments(args);

                Log.Information("=== TradingBot Console gestartet ===");
                LogConfiguration();

                Console.WriteLine("Drücke ENTER um fortzufahren...");
                Console.ReadLine();

                // Client aus Login-Klasse holen
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

                // Konfiguration erstellen
                var configuration = new TradingBotConfiguration
                {
                    BuyCooldown = TimeSpan.FromMinutes(1),
                    SellCooldown = TimeSpan.FromMinutes(1),
                    GlobalCooldown = TimeSpan.FromSeconds(30),
                    SellLockout = TimeSpan.FromMinutes(5),
                    AverageDownEnabled = Login.AverageDownEnabled,
                    BaseInvestmentAmount = Login.MinimalTradingPostionSize
                };

                // Engine konfigurieren und starten
                await engine.ConfigureAsync(configuration);
                await engine.PerformInitialSetupAsync();

                Log.Information("🚀 TradingBot läuft. Drücke ESC zum Beenden...");

                // Warten auf ESC zum Beenden
                while (Console.ReadKey(true).Key != ConsoleKey.Escape)
                {
                    await Task.Delay(100);
                }

                Log.Information("🛑 TradingBot wird beendet...");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Kritischer Fehler in der TradingBot Console");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// Parst alle Kommandozeilenargumente und setzt die Login-Eigenschaften
        /// </summary>
        private static void ParseArguments(string[] args)
        {
            var argDict = new Dictionary<string, string>();

            // Argumente im Format --key=value oder --key value parsen
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--"))
                {
                    var key = args[i].TrimStart('-');
                    string value = null;

                    if (key.Contains('='))
                    {
                        var parts = key.Split('=', 2);
                        key = parts[0];
                        value = parts[1];
                    }
                    else if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        value = args[i + 1];
                        i++;
                    }

                    if (value != null)
                    {
                        argDict[key.ToLower()] = value;
                    }
                }
            }

            // Zeige Hilfe wenn --help angegeben wurde
            if (argDict.ContainsKey("help") || argDict.ContainsKey("h"))
            {
                ShowHelp();
                Environment.Exit(0);
            }

            // FilePreSuffix (API Account Auswahl)
            if (argDict.TryGetValue("prefix", out var prefix) ||
                argDict.TryGetValue("account", out prefix))
            {
                if (Login.ApiPrefixes.Contains(prefix))
                {
                    Login.FilePreSuffix = prefix;
                    Log.Information($"✅ FilePreSuffix: {prefix}");
                }
                else
                {
                    Log.Warning($"⚠️ Ungültiger Prefix '{prefix}'. Verfügbare: {string.Join(", ", Login.ApiPrefixes)}");
                    Log.Warning($"Verwende Standard: '{Login.FilePreSuffix}'");
                }
            }

            // ProfitAccount
            if (argDict.TryGetValue("profitaccount", out var profitAccount))
            {
                Login.ProfitAccount = profitAccount;
                Log.Information($"✅ ProfitAccount: {profitAccount}");
            }

            // MaximalTradingBudget
            if (argDict.TryGetValue("maxbudget", out var maxBudget) &&
                decimal.TryParse(maxBudget, out var maxBudgetValue))
            {
                Login.MaximalTradingBudget = maxBudgetValue;
                Log.Information($"✅ MaximalTradingBudget: {maxBudgetValue:F2} EUR");
            }

            // InitialTradingMultiplier
            if (argDict.TryGetValue("multiplier", out var multiplier) &&
                decimal.TryParse(multiplier, out var multiplierValue))
            {
                Login.InitialTradingMultiplier = multiplierValue;
                Log.Information($"✅ InitialTradingMultiplier: {multiplierValue}");
            }

            // MinimalTradingPostionSize
            if (argDict.TryGetValue("minposition", out var minPos) &&
                decimal.TryParse(minPos, out var minPosValue))
            {
                Login.MinimalTradingPostionSize = minPosValue;
                Log.Information($"✅ MinimalTradingPostionSize: {minPosValue:F2} EUR");
            }

            // VolatilityKindels
            if (argDict.TryGetValue("volatility", out var vol) &&
                int.TryParse(vol, out var volValue))
            {
                Login.VolatilityKindels = volValue;
                Log.Information($"✅ VolatilityKindels: {volValue}");
            }

            // ShouldNotBuyAfterBudget
            if (argDict.TryGetValue("stopafterbudget", out var stopBudget) &&
                bool.TryParse(stopBudget, out var stopBudgetValue))
            {
                Login.ShouldNotBuyAfterBudget = stopBudgetValue;
                Log.Information($"✅ ShouldNotBuyAfterBudget: {stopBudgetValue}");
            }

            // KlineIntervalLength
            if (argDict.TryGetValue("interval", out var interval))
            {
                if (TryParseKlineInterval(interval, out var intervalValue))
                {
                    Login.KlineIntervalLength = intervalValue;
                    Log.Information($"✅ KlineIntervalLength: {intervalValue}");
                }
                else
                {
                    Log.Warning($"⚠️ Ungültiges Interval '{interval}'. Verwende Standard: {Login.KlineIntervalLength}");
                }
            }

            // VolalityConfirmation
            if (argDict.TryGetValue("volatilityconfirm", out var volConfirm) &&
                bool.TryParse(volConfirm, out var volConfirmValue))
            {
                Login.VolalityConfirmation = volConfirmValue;
                Log.Information($"✅ VolalityConfirmation: {volConfirmValue}");
            }

            // AverageDownEnabled
            if (argDict.TryGetValue("averagedown", out var avgDown) &&
                bool.TryParse(avgDown, out var avgDownValue))
            {
                Login.AverageDownEnabled = avgDownValue;
                Log.Information($"✅ AverageDownEnabled: {avgDownValue}");
            }

            // NoSuccessDelay
            if (argDict.TryGetValue("delay", out var delay) &&
                int.TryParse(delay, out var delayValue))
            {
                Login.NoSuccessDelay = delayValue;
                Log.Information($"✅ NoSuccessDelay: {delayValue}ms");
            }

            // InitialSell
            if (argDict.TryGetValue("initialsell", out var initialSell) &&
                bool.TryParse(initialSell, out var initialSellValue))
            {
                Login.InitialSell = initialSellValue;
                Log.Information($"✅ InitialSell: {initialSellValue}");
            }

            // MaxRun
            if (argDict.TryGetValue("maxrun", out var maxRun) &&
                int.TryParse(maxRun, out var maxRunValue))
            {
                Login.MaxRun = maxRunValue;
                Log.Information($"✅ MaxRun: {maxRunValue}");
            }

            // NoActionTakenMinutes
            if (argDict.TryGetValue("noactionminutes", out var noAction) &&
                int.TryParse(noAction, out var noActionValue))
            {
                Login.NoActionTakenMinutes = noActionValue;
                Log.Information($"✅ NoActionTakenMinutes: {noActionValue}");
            }

            // AverageDownStepPercent
            if (argDict.TryGetValue("avgdownstep", out var avgDownStep) &&
                decimal.TryParse(avgDownStep, out var avgDownStepValue))
            {
                Login.AverageDownStepPercent = avgDownStepValue;
                Log.Information($"✅ AverageDownStepPercent: {avgDownStepValue}");
            }
        }

        /// <summary>
        /// Versucht einen String in ein KlineInterval zu parsen
        /// </summary>
        private static bool TryParseKlineInterval(string value, out KlineInterval interval)
        {
            var mapping = new Dictionary<string, KlineInterval>(StringComparer.OrdinalIgnoreCase)
            {
                ["1m"] = KlineInterval.OneMinute,
                ["3m"] = KlineInterval.ThreeMinutes,
                ["5m"] = KlineInterval.FiveMinutes,
                ["15m"] = KlineInterval.FifteenMinutes,
                ["30m"] = KlineInterval.ThirtyMinutes,
                ["1h"] = KlineInterval.OneHour,
                ["2h"] = KlineInterval.TwoHours,
                ["4h"] = KlineInterval.FourHours,
                ["6h"] = KlineInterval.SixHours,
                ["12h"] = KlineInterval.TwelveHours,
                ["1d"] = KlineInterval.OneDay,
                ["1w"] = KlineInterval.OneWeek,
                ["1M"] = KlineInterval.OneMonth
            };

            return mapping.TryGetValue(value, out interval);
        }

        /// <summary>
        /// Zeigt die Hilfe mit allen verfügbaren Argumenten an
        /// </summary>
        private static void ShowHelp()
        {
            Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════════════════════╗
║                      TradingBot Console - Hilfe                           ║
╚═══════════════════════════════════════════════════════════════════════════╝

VERWENDUNG:
  TradingBotConsole [OPTIONEN]

OPTIONEN:
  --prefix <value>           API Account Prefix (Standard: UG3)
                            Verfügbar: '', '1', '2', '3', 'UG', 'UG1', 'UG2', 'UG3'
  
  --profitaccount <value>    Profit Account Name (Standard: PeanutsHosting001)
  
  --maxbudget <decimal>      Maximales Trading Budget in EUR (Standard: 1000)
  
  --multiplier <decimal>     Initial Trading Multiplier (Standard: 2.0)
  
  --minposition <decimal>    Minimale Positionsgröße in EUR (Standard: 10)
  
  --volatility <int>         Anzahl Perioden für Volatilität (Standard: 3)
  
  --stopafterbudget <bool>   Stop buying after budget (Standard: true)
  
  --interval <string>        Kline Interval (Standard: 5m)
                            Verfügbar: 1m, 3m, 5m, 15m, 30m, 1h, 2h, 4h, 6h, 12h, 1d, 1w, 1M
  
  --volatilityconfirm <bool> Volatility Confirmation (Standard: false)
  
  --averagedown <bool>       Average Down Enabled (Standard: false)
  
  --delay <int>              No Success Delay in ms (Standard: 500)
  
  --initialsell <bool>       Initial Sell aktivieren (Standard: true)
  
  --maxrun <int>             Maximale Durchläufe (Standard: 2)
  
  --noactionminutes <int>    Minuten ohne Aktion (Standard: 120)
  
  --avgdownstep <decimal>    Average Down Step Percent (Standard: 0.002)
  
  --help, -h                 Diese Hilfe anzeigen

BEISPIELE:
  
  # Standard-Konfiguration mit Account UG3
  TradingBotConsole --prefix UG3
  
  # Custom Budget und Positionsgröße
  TradingBotConsole --prefix UG2 --maxbudget 2000 --minposition 20
  
  # Mit Average Down und höherem Multiplier
  TradingBotConsole --prefix UG1 --averagedown true --multiplier 3.0
  
  # Mit 15-Minuten Intervall
  TradingBotConsole --prefix UG3 --interval 15m
  
  # Vollständige Custom-Konfiguration
  TradingBotConsole --prefix UG2 --maxbudget 1500 --minposition 15 \
                    --multiplier 2.5 --averagedown true --interval 5m

HINWEISE:
  - Boolean Werte: true/false (case-insensitive)
  - Decimal Werte: Mit Punkt als Dezimaltrennzeichen (z.B. 10.5)
  - Bei ungültigen Werten werden Standardwerte verwendet
");
        }

        /// <summary>
        /// Loggt die aktuelle Konfiguration
        /// </summary>
        private static void LogConfiguration()
        {
            Log.Information("╔═══════════════════════════════════════════════════════════════╗");
            Log.Information("║              Aktuelle Bot-Konfiguration                       ║");
            Log.Information("╚═══════════════════════════════════════════════════════════════╝");
            Log.Information($"FilePreSuffix:              {Login.FilePreSuffix}");
            Log.Information($"Account:                    {Login.GetActualAccount()}");
            Log.Information($"ProfitAccount:              {Login.ProfitAccount}");
            Log.Information($"MaximalTradingBudget:       {Login.MaximalTradingBudget:F2} EUR");
            Log.Information($"InitialTradingMultiplier:   {Login.InitialTradingMultiplier}");
            Log.Information($"MinimalTradingPostionSize:  {Login.MinimalTradingPostionSize:F2} EUR");
            Log.Information($"VolatilityKindels:          {Login.VolatilityKindels}");
            Log.Information($"ShouldNotBuyAfterBudget:    {Login.ShouldNotBuyAfterBudget}");
            Log.Information($"KlineIntervalLength:        {Login.KlineIntervalLength}");
            Log.Information($"VolalityConfirmation:       {Login.VolalityConfirmation}");
            Log.Information($"AverageDownEnabled:         {Login.AverageDownEnabled}");
            Log.Information($"NoSuccessDelay:             {Login.NoSuccessDelay}ms");
            Log.Information($"InitialSell:                {Login.InitialSell}");
            Log.Information($"MaxRun:                     {Login.MaxRun}");
            Log.Information($"NoActionTakenMinutes:       {Login.NoActionTakenMinutes}");
            Log.Information($"AverageDownStepPercent:     {Login.AverageDownStepPercent}");
            Log.Information($"RunTradingLoop:             {Login.RunTradingLoop}");
            Log.Information("╚═══════════════════════════════════════════════════════════════╝");
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