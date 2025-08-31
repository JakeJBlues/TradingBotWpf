using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace TradingBotCore.Manager
{
    // Thread-sichere Cooldown-Verwaltung mit Verkaufssperre
    public class TradingCooldownManager
    {
        private readonly ConcurrentDictionary<string, DateTime> _lastBuyTimes = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastSellTimes = new();
        private readonly ConcurrentDictionary<string, DateTime> _sellLockoutTimes = new();
        private readonly TimeSpan _buyDelay;
        private readonly TimeSpan _sellDelay;
        private readonly TimeSpan _globalCooldown;
        private readonly TimeSpan _sellLockoutDuration;
        private DateTime _lastGlobalAction = DateTime.MinValue;
        private readonly object _globalLock = new object();

        public TradingCooldownManager(TimeSpan buyDelay, TimeSpan sellDelay, TimeSpan globalCooldown, TimeSpan sellLockoutDuration)
        {
            _buyDelay = buyDelay;
            _sellDelay = sellDelay;
            _globalCooldown = globalCooldown;
            _sellLockoutDuration = sellLockoutDuration;
        }

        public bool CanBuy(string symbol)
        {
            var now = DateTime.UtcNow;

            // Prüfe Verkaufssperre - verhindert Kauf nach Verkauf
            if (_sellLockoutTimes.TryGetValue(symbol, out var sellLockoutTime))
            {
                var timeSinceSellLockout = now - sellLockoutTime;
                if (timeSinceSellLockout < _sellLockoutDuration)
                {
                    Log.Debug($"Verkaufssperre für {symbol} aktiv. Verbleibend: {(_sellLockoutDuration - timeSinceSellLockout).TotalMinutes:F1} Minuten");
                    return false;
                }
            }

            // Globaler Cooldown zwischen allen Aktionen
            lock (_globalLock)
            {
                if (now - _lastGlobalAction < _globalCooldown)
                {
                    Log.Debug($"Globaler Cooldown aktiv. Verbleibend: {(_globalCooldown - (now - _lastGlobalAction)).TotalSeconds:F1}s");
                    return false;
                }
            }

            // Symbol-spezifischer Kauf-Cooldown
            if (_lastBuyTimes.TryGetValue(symbol, out var lastBuy))
            {
                var timeSinceLastBuy = now - lastBuy;
                if (timeSinceLastBuy < _buyDelay)
                {
                    Log.Debug($"Kauf-Cooldown für {symbol} aktiv. Verbleibend: {(_buyDelay - timeSinceLastBuy).TotalSeconds:F1}s");
                    return false;
                }
            }

            return true;
        }

        public bool CanSell(string symbol)
        {
            var now = DateTime.UtcNow;

            // Globaler Cooldown
            lock (_globalLock)
            {
                if (now - _lastGlobalAction < _globalCooldown)
                {
                    Log.Debug($"Globaler Cooldown aktiv für Verkauf. Verbleibend: {(_globalCooldown - (now - _lastGlobalAction)).TotalSeconds:F1}s");
                    return false;
                }
            }

            // Symbol-spezifischer Verkauf-Cooldown
            if (_lastSellTimes.TryGetValue(symbol, out var lastSell))
            {
                var timeSinceLastSell = now - lastSell;
                if (timeSinceLastSell < _sellDelay)
                {
                    Log.Debug($"Verkauf-Cooldown für {symbol} aktiv. Verbleibend: {(_sellDelay - timeSinceLastSell).TotalSeconds:F1}s");
                    return false;
                }
            }
            // Log.Debug($"Kein Verkauf-Cooldown für {symbol} aktiv.");
            return true;
        }

        public void RecordBuy(string symbol)
        {
            var now = DateTime.UtcNow;
            _lastBuyTimes.AddOrUpdate(symbol, now, (key, oldValue) => now);
            _lastSellTimes[symbol] = now; // Setze letzten Verkauf auf jetzt, um Verkaufssperre zu aktivieren

            lock (_globalLock)
            {
                _lastGlobalAction = now;
            }

            Log.Information($"Kauf-Cooldown für {symbol} aktiviert bis {now.Add(_buyDelay):HH:mm:ss}");
        }

        public void RecordSell(string symbol)
        {
            var now = DateTime.UtcNow;
            _lastSellTimes.AddOrUpdate(symbol, now, (key, oldValue) => now);

            // Verkaufssperre aktivieren - verhindert Neukauf für 30 Minuten
            _sellLockoutTimes.AddOrUpdate(symbol, now, (key, oldValue) => now);

            lock (_globalLock)
            {
                _lastGlobalAction = now;
            }

            Log.Information($"Verkauf-Cooldown für {symbol} aktiviert bis {now.Add(_sellDelay):HH:mm:ss}");
            Log.Information($"Verkaufssperre für {symbol} aktiviert bis {now.Add(_sellLockoutDuration):HH:mm:ss} - Kein Neukauf möglich!");
        }

        public Dictionary<string, TimeSpan> GetActiveLockouts()
        {
            var now = DateTime.UtcNow;
            var activeLockouts = new Dictionary<string, TimeSpan>();

            foreach (var kvp in _sellLockoutTimes)
            {
                var timeSinceLockout = now - kvp.Value;
                var remainingTime = _sellLockoutDuration - timeSinceLockout;

                if (remainingTime > TimeSpan.Zero)
                {
                    activeLockouts[kvp.Key] = remainingTime;
                }
            }

            return activeLockouts;
        }

        public void CleanupOldEntries(TimeSpan maxAge)
        {
            var cutoff = DateTime.UtcNow - maxAge;

            var oldBuyKeys = _lastBuyTimes
                .Where(kvp => kvp.Value < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            var oldSellKeys = _lastSellTimes
                .Where(kvp => kvp.Value < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            var oldLockoutKeys = _sellLockoutTimes
                .Where(kvp => kvp.Value < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in oldBuyKeys)
                _lastBuyTimes.TryRemove(key, out _);

            foreach (var key in oldSellKeys)
                _lastSellTimes.TryRemove(key, out _);

            foreach (var key in oldLockoutKeys)
                _sellLockoutTimes.TryRemove(key, out _);

            Log.Debug($"Cooldown-Cleanup: {oldBuyKeys.Count + oldSellKeys.Count + oldLockoutKeys.Count} alte Einträge entfernt");
        }
    }
}