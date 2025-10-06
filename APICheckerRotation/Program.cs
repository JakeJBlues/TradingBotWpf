using TradingBot;
using TradingBotCore;
using TradingBotCore.Helper;
using TradingBotCore.Manager;

foreach (var api in Login.ApiPrefixes)
{
    Console.WriteLine($"Starte mit API: {api}");
    Login.FilePreSuffix = api;
    Login.InitialSell = false;
    Console.WriteLine($"FilePreSuffix gesetzt auf: {Login.FilePreSuffix}");
    var cooldownManager = new EnhancedTradingCooldownManager(
              TimeSpan.FromMinutes(10),
              TimeSpan.FromSeconds(1),  // Geändert von 0!
              TimeSpan.FromMilliseconds(30), // Geändert von 1 Mikrosekunde!
              TimeSpan.FromMinutes(60)
          );
    Console.WriteLine("CooldownManager erstellt.");
    var positionManager = new PositionManager();
    Console.WriteLine("PositionManager erstellt.");
    var profitTracker = new ProtectedProfitTracker();
    Console.WriteLine("ProfitTracker erstellt.");
    var blacklistManager = new TradingBlacklistManager();
    Console.WriteLine("BlacklistManager erstellt.");

    var config = new TradingBotCore.Entitites.TradingBotConfiguration();

    var botEngine = new TradingBotEngine(cooldownManager, positionManager, profitTracker, blacklistManager);
    await botEngine.ConfigureAsync(config);
    Console.WriteLine("BotEngine konfiguriert.");
    await botEngine.PerformInitialSetupAsync();
    Console.WriteLine($"Fertig mit API: {api}");
    await Task.Delay(2000);
}