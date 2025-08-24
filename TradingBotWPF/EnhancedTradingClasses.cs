using System;
using System.ComponentModel;
using System.Windows.Media;

namespace TradingBotWPF
{
    /// <summary>
    /// Live-Marktdaten für Dashboard
    /// </summary>
    public class LiveMarketData : INotifyPropertyChanged
    {
        private decimal _btcPrice;
        private decimal _ethPrice;
        private decimal _totalMarketCap;
        private decimal _marketFear;
        private string _marketSentiment;
        private DateTime _lastUpdate;

        public decimal BtcPrice
        {
            get => _btcPrice;
            set
            {
                _btcPrice = value;
                OnPropertyChanged(nameof(BtcPrice));
                OnPropertyChanged(nameof(BtcPriceFormatted));
            }
        }

        public decimal EthPrice
        {
            get => _ethPrice;
            set
            {
                _ethPrice = value;
                OnPropertyChanged(nameof(EthPrice));
                OnPropertyChanged(nameof(EthPriceFormatted));
            }
        }

        public decimal TotalMarketCap
        {
            get => _totalMarketCap;
            set
            {
                _totalMarketCap = value;
                OnPropertyChanged(nameof(TotalMarketCap));
                OnPropertyChanged(nameof(TotalMarketCapFormatted));
            }
        }

        public decimal MarketFear
        {
            get => _marketFear;
            set
            {
                _marketFear = value;
                OnPropertyChanged(nameof(MarketFear));
                OnPropertyChanged(nameof(MarketFearFormatted));
                OnPropertyChanged(nameof(MarketFearColor));
            }
        }

        public string MarketSentiment
        {
            get => _marketSentiment;
            set
            {
                _marketSentiment = value;
                OnPropertyChanged(nameof(MarketSentiment));
                OnPropertyChanged(nameof(MarketSentimentIcon));
            }
        }

        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set
            {
                _lastUpdate = value;
                OnPropertyChanged(nameof(LastUpdate));
                OnPropertyChanged(nameof(LastUpdateFormatted));
            }
        }

        // Formatierte Properties
        public string BtcPriceFormatted => $"₿ {BtcPrice:F0} EUR";
        public string EthPriceFormatted => $"Ξ {EthPrice:F0} EUR";
        public string TotalMarketCapFormatted => $"{TotalMarketCap / 1_000_000_000:F1}B EUR";
        public string MarketFearFormatted => $"Fear Index: {MarketFear:F0}";
        public string LastUpdateFormatted => $"Update: {LastUpdate:HH:mm:ss}";

        public string MarketSentimentIcon => MarketSentiment switch
        {
            "Bullish" => "🐂",
            "Bearish" => "🐻",
            "Neutral" => "😐",
            _ => "❓"
        };

        public Brush MarketFearColor => MarketFear switch
        {
            <= 25 => Brushes.Red,      // Extreme Fear
            <= 45 => Brushes.Orange,   // Fear
            <= 55 => Brushes.Yellow,   // Neutral
            <= 75 => Brushes.LightGreen, // Greed
            _ => Brushes.Green         // Extreme Greed
        };

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Trading-Performance-Metriken für Live-Anzeige
    /// </summary>
    public class LiveTradingMetrics : INotifyPropertyChanged
    {
        private int _totalTradesToday;
        private decimal _profitToday;
        private decimal _bestPerformingPosition;
        private decimal _worstPerformingPosition;
        private string _bestSymbol;
        private string _worstSymbol;
        private TimeSpan _averageHoldTime;
        private decimal _winRate;

        public int TotalTradesToday
        {
            get => _totalTradesToday;
            set
            {
                _totalTradesToday = value;
                OnPropertyChanged(nameof(TotalTradesToday));
            }
        }

        public decimal ProfitToday
        {
            get => _profitToday;
            set
            {
                _profitToday = value;
                OnPropertyChanged(nameof(ProfitToday));
                OnPropertyChanged(nameof(ProfitTodayFormatted));
                OnPropertyChanged(nameof(ProfitTodayColor));
            }
        }

        public decimal BestPerformingPosition
        {
            get => _bestPerformingPosition;
            set
            {
                _bestPerformingPosition = value;
                OnPropertyChanged(nameof(BestPerformingPosition));
                OnPropertyChanged(nameof(BestPerformanceFormatted));
            }
        }

        public decimal WorstPerformingPosition
        {
            get => _worstPerformingPosition;
            set
            {
                _worstPerformingPosition = value;
                OnPropertyChanged(nameof(WorstPerformingPosition));
                OnPropertyChanged(nameof(WorstPerformanceFormatted));
            }
        }

        public string BestSymbol
        {
            get => _bestSymbol;
            set
            {
                _bestSymbol = value;
                OnPropertyChanged(nameof(BestSymbol));
                OnPropertyChanged(nameof(BestPerformanceFormatted));
            }
        }

        public string WorstSymbol
        {
            get => _worstSymbol;
            set
            {
                _worstSymbol = value;
                OnPropertyChanged(nameof(WorstSymbol));
                OnPropertyChanged(nameof(WorstPerformanceFormatted));
            }
        }

        public TimeSpan AverageHoldTime
        {
            get => _averageHoldTime;
            set
            {
                _averageHoldTime = value;
                OnPropertyChanged(nameof(AverageHoldTime));
                OnPropertyChanged(nameof(AverageHoldTimeFormatted));
            }
        }

        public decimal WinRate
        {
            get => _winRate;
            set
            {
                _winRate = value;
                OnPropertyChanged(nameof(WinRate));
                OnPropertyChanged(nameof(WinRateFormatted));
                OnPropertyChanged(nameof(WinRateColor));
            }
        }

        // Formatierte Properties
        public string ProfitTodayFormatted => $"{ProfitToday:F2} EUR";
        public string BestPerformanceFormatted => string.IsNullOrEmpty(BestSymbol) ? "Keine Daten" : $"{BestSymbol}: +{BestPerformingPosition:F2}%";
        public string WorstPerformanceFormatted => string.IsNullOrEmpty(WorstSymbol) ? "Keine Daten" : $"{WorstSymbol}: {WorstPerformingPosition:F2}%";
        public string AverageHoldTimeFormatted => $"{AverageHoldTime.TotalHours:F1}h";
        public string WinRateFormatted => $"{WinRate:F1}%";

        public Brush ProfitTodayColor => ProfitToday >= 0 ? Brushes.Green : Brushes.Red;
        public Brush WinRateColor => WinRate switch
        {
            >= 70 => Brushes.Green,
            >= 50 => Brushes.Orange,
            _ => Brushes.Red
        };

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// API-Verbindungsstatus für Monitoring
    /// </summary>
    public class ApiConnectionStatus : INotifyPropertyChanged
    {
        private bool _isConnected;
        private DateTime _lastSuccessfulCall;
        private int _failedCallsToday;
        private TimeSpan _averageResponseTime;
        private string _lastError;

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectionStatusText));
                OnPropertyChanged(nameof(ConnectionStatusColor));
                OnPropertyChanged(nameof(ConnectionStatusIcon));
            }
        }

        public DateTime LastSuccessfulCall
        {
            get => _lastSuccessfulCall;
            set
            {
                _lastSuccessfulCall = value;
                OnPropertyChanged(nameof(LastSuccessfulCall));
                OnPropertyChanged(nameof(LastSuccessfulCallFormatted));
            }
        }

        public int FailedCallsToday
        {
            get => _failedCallsToday;
            set
            {
                _failedCallsToday = value;
                OnPropertyChanged(nameof(FailedCallsToday));
            }
        }

        public TimeSpan AverageResponseTime
        {
            get => _averageResponseTime;
            set
            {
                _averageResponseTime = value;
                OnPropertyChanged(nameof(AverageResponseTime));
                OnPropertyChanged(nameof(AverageResponseTimeFormatted));
                OnPropertyChanged(nameof(ResponseTimeColor));
            }
        }

        public string LastError
        {
            get => _lastError;
            set
            {
                _lastError = value;
                OnPropertyChanged(nameof(LastError));
            }
        }

        // Formatierte Properties
        public string ConnectionStatusText => IsConnected ? "Verbunden" : "Getrennt";
        public string ConnectionStatusIcon => IsConnected ? "🟢" : "🔴";
        public string LastSuccessfulCallFormatted => $"Letzter Call: {LastSuccessfulCall:HH:mm:ss}";
        public string AverageResponseTimeFormatted => $"~{AverageResponseTime.TotalMilliseconds:F0}ms";

        public Brush ConnectionStatusColor => IsConnected ? Brushes.Green : Brushes.Red;
        public Brush ResponseTimeColor => AverageResponseTime.TotalMilliseconds switch
        {
            <= 100 => Brushes.Green,
            <= 500 => Brushes.Orange,
            _ => Brushes.Red
        };

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Live-Alerting-System für wichtige Events
    /// </summary>
    public class LiveTradingAlert : INotifyPropertyChanged
    {
        public enum AlertType
        {
            Info,
            Warning,
            Error,
            Success,
            PriceAlert,
            PositionAlert
        }

        private AlertType _type;
        private string _message;
        private string _symbol;
        private DateTime _timestamp;
        private bool _isRead;

        public AlertType Type
        {
            get => _type;
            set
            {
                _type = value;
                OnPropertyChanged(nameof(Type));
                OnPropertyChanged(nameof(TypeIcon));
                OnPropertyChanged(nameof(TypeColor));
            }
        }

        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                OnPropertyChanged(nameof(Message));
            }
        }

        public string Symbol
        {
            get => _symbol;
            set
            {
                _symbol = value;
                OnPropertyChanged(nameof(Symbol));
            }
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                _timestamp = value;
                OnPropertyChanged(nameof(Timestamp));
                OnPropertyChanged(nameof(TimestampFormatted));
                OnPropertyChanged(nameof(TimeAgo));
            }
        }

        public bool IsRead
        {
            get => _isRead;
            set
            {
                _isRead = value;
                OnPropertyChanged(nameof(IsRead));
                OnPropertyChanged(nameof(ReadIndicator));
            }
        }

        // Formatierte Properties
        public string TypeIcon => Type switch
        {
            AlertType.Info => "ℹ️",
            AlertType.Warning => "⚠️",
            AlertType.Error => "❌",
            AlertType.Success => "✅",
            AlertType.PriceAlert => "📈",
            AlertType.PositionAlert => "📊",
            _ => "❓"
        };

        public Brush TypeColor => Type switch
        {
            AlertType.Info => Brushes.Blue,
            AlertType.Warning => Brushes.Orange,
            AlertType.Error => Brushes.Red,
            AlertType.Success => Brushes.Green,
            AlertType.PriceAlert => Brushes.Purple,
            AlertType.PositionAlert => Brushes.DarkBlue,
            _ => Brushes.Gray
        };

        public string TimestampFormatted => Timestamp.ToString("HH:mm:ss");
        public string TimeAgo
        {
            get
            {
                var span = DateTime.UtcNow - Timestamp;
                if (span.TotalMinutes < 1) return "Gerade eben";
                if (span.TotalMinutes < 60) return $"vor {span.TotalMinutes:F0} Min";
                if (span.TotalHours < 24) return $"vor {span.TotalHours:F0}h";
                return $"vor {span.TotalDays:F0} Tagen";
            }
        }

        public string ReadIndicator => IsRead ? "" : "🔴";

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

/// <summary>
/// Erweiterte Portfolio-Übersicht mit Live-Daten
/// </summary>
public class LivePortfolioSummary : INotifyPropertyChanged
{
    private decimal _totalPortfolioValue;
    private decimal _totalUnrealizedPL;
    private decimal _totalUnrealizedPLPercent;
    private int _positionsInProfit;
    private int _positionsInLoss;
    private DateTime _lastUpdate;

    public decimal TotalPortfolioValue
    {
        get => _totalPortfolioValue;
        set
        {
            _totalPortfolioValue = value;
            OnPropertyChanged(nameof(TotalPortfolioValue));
            OnPropertyChanged(nameof(TotalPortfolioValueFormatted));
        }
    }

    public decimal TotalUnrealizedPL
    {
        get => _totalUnrealizedPL;
        set
        {
            _totalUnrealizedPL = value;
            OnPropertyChanged(nameof(TotalUnrealizedPL));
            OnPropertyChanged(nameof(TotalUnrealizedPLFormatted));
            OnPropertyChanged(nameof(TotalPLColor));
        }
    }

    public decimal TotalUnrealizedPLPercent
    {
        get => _totalUnrealizedPLPercent;
        set
        {
            _totalUnrealizedPLPercent = value;
            OnPropertyChanged(nameof(TotalUnrealizedPLPercent));
            OnPropertyChanged(nameof(TotalUnrealizedPLPercentFormatted));
        }
    }

    public int PositionsInProfit
    {
        get => _positionsInProfit;
        set
        {
            _positionsInProfit = value;
            OnPropertyChanged(nameof(PositionsInProfit));
            OnPropertyChanged(nameof(ProfitLossRatio));
        }
    }

    public int PositionsInLoss
    {
        get => _positionsInLoss;
        set
        {
            _positionsInLoss = value;
            OnPropertyChanged(nameof(PositionsInLoss));
            OnPropertyChanged(nameof(ProfitLossRatio));
        }
    }

    public DateTime LastUpdate
    {
        get => _lastUpdate;
        set
        {
            _lastUpdate = value;
            OnPropertyChanged(nameof(LastUpdate));
            OnPropertyChanged(nameof(LastUpdateFormatted));
        }
    }

    // Formatierte Properties
    public string TotalPortfolioValueFormatted => $"{TotalPortfolioValue:F2} EUR";
    public string TotalUnrealizedPLFormatted => $"{TotalUnrealizedPL:F2} EUR";
    public string TotalUnrealizedPLPercentFormatted => $"{TotalUnrealizedPLPercent:F2}%";
    public string LastUpdateFormatted => $"Update: {LastUpdate:HH:mm:ss}";
    public string ProfitLossRatio => $"{PositionsInProfit}🟢 / {PositionsInLoss}🔴";

    public Brush TotalPLColor => TotalUnrealizedPL >= 0 ? Brushes.Green : Brushes.Red;

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>