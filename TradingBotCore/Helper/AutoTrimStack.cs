using System.Collections.Generic;

namespace TradingBotCore.Helper
{
    // Dummy AutoTrimStack und EmaBollingerStrategy für Kompatibilität
    public class AutoTrimStack<T>
    {
        private readonly Queue<T> _items;
        private readonly int _maxSize;

        public AutoTrimStack(int maxSize)
        {
            _maxSize = maxSize;
            _items = new Queue<T>();
        }

        public void Push(T item)
        {
            _items.Enqueue(item);
            while (_items.Count > _maxSize)
            {
                _items.Dequeue();
            }
        }
    }
}