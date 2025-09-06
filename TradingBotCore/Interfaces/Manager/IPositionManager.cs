using System.Collections.Concurrent;
using TradingBotCore.Entities;

namespace TradingBotCore.Interfaces.Manager
{
    public interface IPositionManager
    {
        ConcurrentDictionary<string, TradingPosition> _positions { get; set; }
        DateTime LastTransaction { get; set; }

        void AddOrUpdatePosition(TradingPosition position);
        double CalculateGreenRatio();
        List<string> GetAssetsWithPositions();
        TradingPosition GetPosition(string symbol);
        TradingPosition GetPositionByAsset(string asset);
        TradingPosition GetPositionBySymbol(string symbol);
        int GetPositionCount();
        List<TradingPosition> GetPositions();
        bool HasPositionForAsset(string symbol);
        bool NoActionRecorded();
        void RemovePosition(TradingPosition position);
    }
}