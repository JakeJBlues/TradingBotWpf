using OKX.Net.Clients;

namespace TradingBotCore.Interfaces
{
    public interface IOrderPriceHelper
    {
        static abstract Task<(bool Success, decimal ActualPrice, string OrderId)> ExecuteBuyOrderWithActualPriceAsync(OKXRestClient client, string symbol, decimal investmentAmountEUR);
        static abstract Task<decimal> GetActualPriceFromOrderAsync(OKXRestClient client, string orderId);
        static abstract Task<decimal> GetActualPriceFromOrderWithRetryAsync(OKXRestClient client, string orderId, int maxRetries = 3);
    }
}