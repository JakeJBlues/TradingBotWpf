using CryptoExchange.Net.Authentication;
using OKX.Net;
using OKX.Net.Clients;
using OKX.Net.Enums;
using OKX.Net.Objects.Market;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingBotCore
{
    public class Login
    {
        public const string FilePreSuffix = "3";
        public const decimal MaximalTradingBudget = 500m;
        public const decimal MinimalTradingPostionSize = 10m; // Minimale Positionsgröße in EUR
        public const int VolatilityKindels = 5; // Anzahl der Perioden für die Volatilitätsberechnung
        public const bool ShouldBuyAfterBudget = false;
        public const KlineInterval KlineIntervalLength = KlineInterval.OneMinute;
        public const bool VolalityConfirmation = false;
        public const bool AverageDownEnabled = true;
        public const int NoSuccessDelay = 2000;
        public const int MaxRun = 3;
        public static int BudgetDownCounter { get;private set; } = 0;

        public static bool RunTradingLoop => BudgetDownCounter < MaxRun;

        public static OKXRestClient Credentials()
        {
            return Credentials(FilePreSuffix);
        }

        public static OKXRestClient Credentials(string prefix)
        {
            switch (prefix)
            {
                case "":
                    return new OKXRestClient(options =>
                    {
                        options.ApiCredentials = new ApiCredentials("c77c9afa-fed2-4eba-915c-f8d4eb23aba2", "63686C8CB72F797FB94CB63E5E1A6776", "oGlennyweg2311!x");
                        options.Environment = OKXEnvironment.Europe;
                    });
                case "1":
                    return new OKXRestClient(options =>
                    {
                        options.ApiCredentials = new ApiCredentials("6c93c21c-72c2-4cdb-9859-3d243b4c22dc", "11849456529176287887A228CF37B3B8", "oGlennyweg2311!x");
                        options.Environment = OKXEnvironment.Europe;
                    });
                case "2":
                    return new OKXRestClient(options =>
                    {
                        options.ApiCredentials = new ApiCredentials("424dadba-f9ab-4f80-9033-1f6b45af13d8", "A5A0BB32E7D9D0B84E31B673652AFC87", "oGlennyweg2311!x");
                        options.Environment = OKXEnvironment.Europe;
                    });
                case "3":
                    return new OKXRestClient(options =>
                    {
                        options.ApiCredentials = new ApiCredentials("8c8ea1b7-1323-4883-b749-671aa3ac76fe", "808DB7EDD897AADEFB908D948563B579", "oGlennyweg2311!x");
                        options.Environment = OKXEnvironment.Europe;
                    });
                default:
                    throw new Exception("No valid prefix provided!" + prefix);
            }
        }

        public static void IncreaseBudgetDownCounter()
        {
            BudgetDownCounter++;
        }


    }
}

