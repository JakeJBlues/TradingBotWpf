using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingBotCore.Entitites
{
    public class TradingBotConfiguration
    {
        public string ApiKey { get; set; }
        public string Secret { get; set; }
        public string Passphrase { get; set; }
        public TimeSpan BuyCooldown { get; set; }
        public TimeSpan SellCooldown { get; set; }
        public TimeSpan GlobalCooldown { get; set; }
        public TimeSpan SellLockout { get; set; }
        public bool AverageDownEnabled { get; set; } = true;
        public decimal BaseInvestmentAmount { get; set; } = 10.0m;
    }
}
