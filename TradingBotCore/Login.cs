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
        public static List<string> ApiPrefixes = new List<string> { "", "1", "2", "3", "UG", "UG1", "UG2", "UG3" };
        public static string FilePreSuffix = "UG3";
        public static string ProfitAccount = "PeanutsHosting001";//"PeanutsHostingAPI001";
        public static decimal MaximalTradingBudget = 1000m;
        public static decimal InitialTradingMultiplier = 2.0m; // Start-Multiplikator für die Positionsgröße
        public static decimal MinimalTradingPostionSize = 10m; // Minimale Positionsgröße in EUR
        public static int VolatilityKindels = 3; // Anzahl der Perioden für die Volatilitätsberechnung
        public static bool ShouldNotBuyAfterBudget = true;
        public static KlineInterval KlineIntervalLength = KlineInterval.FiveMinutes;
        public static bool VolalityConfirmation = false;
        public static bool AverageDownEnabled = false;
        public static int NoSuccessDelay = 500;
        public static bool InitialSell = true;
        public static int MaxRun = 2;
        public static int NoActionTakenMinutes = 120;
        public static decimal AverageDownStepPercent = 0.002m;
        public static int BudgetDownCounter { get; private set; } = 0;

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
                case "UG":
                    return new OKXRestClient(options =>
                    {
                        options.ApiCredentials = new ApiCredentials("5b4f8e67-1a89-4a4e-8c6c-8078b2e70e49", "6661373BA42DC9CBFC6079DFB20BBAAA", "oGlennyweg2311!x");
                        options.Environment = OKXEnvironment.Europe;
                    });
                case "UG1":
                    return new OKXRestClient(options =>
                    {
                        options.ApiCredentials = new ApiCredentials("93f817a9-9a84-45ae-bea7-8acba6be5929", "D1569CEF469191720979AF5AE871B19D", "oGlennyweg2311!x");
                        options.Environment = OKXEnvironment.Europe;
                    });
                case "UG2":
                    return new OKXRestClient(options =>
                    {
                        options.ApiCredentials = new ApiCredentials("d4b6e08b-4bcb-4e1f-86a3-059a7497a2e0", "BD830CC5FB7AEBE4A6B0949A0690FA08", "oGlennyweg2311!x");
                        options.Environment = OKXEnvironment.Europe;
                    });
                case "UG3":
                    return new OKXRestClient(options =>
                    {
                        options.ApiCredentials = new ApiCredentials("debe740d-2fd8-4543-9726-682aecad45b1", "5E6833062E9DE2B2628DC8C5B1441662", "oGlennyweg2311!x");
                        options.Environment = OKXEnvironment.Europe;
                    });
                default:
                    throw new Exception("No valid prefix provided!" + prefix);
            }
        }

        public static string GetActualAccount()
        {
            switch (Login.FilePreSuffix)
            {
                case "":
                    return "MainAccount";
                case "1":
                    return "JakeJBluesAPI001";
                case "2":
                    return "JakeJBluesAPI002";
                case "3":
                    return "JakeJBluesAPI003";
                case "UG":
                    return "MainAccount";
                case "UG1":
                    return "PeanutsHostingAPI002";
                case "UG2":
                    return "PeanutsHostingAPI003";
                case "UG3":
                    return "PeanutsHostingAPI004";
                default:
                    throw new Exception("No valid prefix provided!" + FilePreSuffix);
            }
        }

        public static void IncreaseBudgetDownCounter()
        {
            BudgetDownCounter++;
        }


    }
}

