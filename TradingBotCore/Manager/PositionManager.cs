using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Serilog;
using TradingBotCore.Entities;
using TradingBotCore.Interfaces.Manager;

namespace TradingBotCore.Manager
{
    // PositionManager für nur eine Position pro Krypto
    public class PositionManager : IPositionManager
    {
        public ConcurrentDictionary<string, TradingPosition> _positions { get; set; } = new();
        public DateTime LastTransaction { get; set; } = DateTime.MinValue;
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
        public void AddOrUpdatePosition(TradingPosition position)
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

        public List<TradingPosition> GetPositions()
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
            if (_positions.Count == 0)
            {
                return 0;
            }
            if (NoActionRecorded())
            {
                return 0.0065;
            }
            if (_positions.Count < 10)
            {
                return 0.01;
            }
            var greenCount = _positions.Values.Count(p => p.CurrentMarketPrice >= p.OriginalPurchasePrice);
            var greenRatio = (double)greenCount / _positions.Count;
            if (greenRatio < 0.25)
            {
                return 0.0075;
            }
            if (greenRatio < 0.5)
            {
                return 0.01;
            }
            if (greenRatio < 0.75)
            {
                return 0.0125;
            }
            return 0.015;

        }

        public bool NoActionRecorded()
        {
            _lock.EnterReadLock();
            try
            {
                return DateTime.Now.Subtract(LastTransaction).Minutes >= Login.NoActionTakenMinutes;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}