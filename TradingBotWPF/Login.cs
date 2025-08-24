using CryptoExchange.Net.Authentication;
using OKX.Net;
using OKX.Net.Clients;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingBotWPF
{
    public class Login
    {
        public static OKXRestClient Credentials()
        {
            return new OKXRestClient(options =>
            {
                options.ApiCredentials = new ApiCredentials("c77c9afa-fed2-4eba-915c-f8d4eb23aba2", "63686C8CB72F797FB94CB63E5E1A6776", "oGlennyweg2311!x");
                options.Environment = OKXEnvironment.Europe;
            });
        }
    }
}
