using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Serilog;
using TradingBotWPF.Entities;

namespace TradingBotWPF.Manager
{
    // PositionManager für nur eine Position pro Krypto
    public class PositionManager
    {
        private readonly ConcurrentDictionary<string, EnhancedTradingPosition> _positions = new();
        private DateTime LastTransaction { get; set; } = DateTime.MinValue;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        // Prüft ob bereits eine Position für das Asset existiert
        public bool HasPositionForAsset(string symbol)
        {
            var asset = ExtractAssetFromSymbol(symbol);
            _lock.EnterReadLock();
            try
            {
                return _positions.ContainsKey(asset);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        // AddOrUpdate statt Add - überschreibt existierende Position
        public void AddOrUpdatePosition(EnhancedTradingPosition position)
        {
            var asset = ExtractAssetFromSymbol(position.Symbol);
            _lock.EnterWriteLock();
            try
            {
                var wasUpdate = _positions.ContainsKey(asset);
                _positions[asset] = position;

                if (wasUpdate)
                {
                    Log.Information($"Position aktualisiert: {position.Symbol} bei {position.High} (nur eine Position pro Asset erlaubt)");
                }
                else
                {
                    Log.Information($"Position hinzugefügt: {position.Symbol} bei {position.High}");
                    LastTransaction = DateTime.UtcNow;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        // RemovePosition über Asset-Key
        public void RemovePosition(TradingPosition position)
        {
            var asset = ExtractAssetFromSymbol(position.Symbol);
            _lock.EnterWriteLock();
            try
            {
                if (_positions.TryRemove(asset, out var removedPosition))
                {
                    Log.Information($"Position entfernt: {removedPosition.Symbol}");
                    LastTransaction = DateTime.UtcNow;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public List<EnhancedTradingPosition> GetPositions()
        {
            _lock.EnterReadLock();
            try
            {
                return _positions.Values.ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public TradingPosition GetPosition(string symbol)
        {
            return GetPositionBySymbol(symbol);
        }

        public TradingPosition GetPositionBySymbol(string symbol)
        {
            var asset = ExtractAssetFromSymbol(symbol);
            return GetPositionByAsset(asset);
        }

        public TradingPosition GetPositionByAsset(string asset)
        {
            _lock.EnterReadLock();
            try
            {
                _positions.TryGetValue(asset.ToUpper(), out var position);
                return position;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        // HILFSMETHODE: Extrahiert Asset aus Symbol (z.B. "BTC-EUR" -> "BTC")
        private string ExtractAssetFromSymbol(string symbol)
        {
            var parts = symbol.ToUpper().Split('-');
            return parts.Length > 0 ? parts[0] : symbol.ToUpper();
        }

        public int GetPositionCount()
        {
            _lock.EnterReadLock();
            try
            {
                return _positions.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public List<string> GetAssetsWithPositions()
        {
            _lock.EnterReadLock();
            try
            {
                return _positions.Keys.ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public double CalculateGreenRatio()
        {
            _lock.EnterReadLock();
            try
            {
                if (_positions.Count == 0) return 0;
                if (NoActionRecorded())
                {
                    return 0.003;
                }
                var greenCount = _positions.Values.Count(p => p.CurrentMarketPrice >= p.OriginalPurchasePrice);
                var greenRatio = (double)greenCount / _positions.Count * 100;
                if (greenRatio < 0.25)
                {
                    return 0.004;
                }
                if (greenRatio < 0.5)
                {
                    return 0.005;
                }
                if (greenRatio < 0.75)
                {
                    return 0.0075;
                }
                return 0.01;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool NoActionRecorded()
        {
            _lock.EnterReadLock();
            try
            {
                return LastTransaction.Subtract(DateTime.Now.AddMinutes(-30)).Nanoseconds > 0;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}