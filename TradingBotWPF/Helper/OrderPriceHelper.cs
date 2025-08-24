using System;
using System.Threading.Tasks;
using OKX.Net.Clients;
using OKX.Net.Enums;
using Serilog;

namespace TradingBotWPF.Helper
{
    /// <summary>
    /// Einfache Integration von Methode 1 in Ihren bestehenden Bot
    /// </summary>
    public static class OrderPriceHelper
    {
        /// <summary>
        /// Führt Buy-Order aus und ermittelt den tatsächlichen Kaufpreis (Methode 1)
        /// </summary>
        public static async Task<(bool Success, decimal ActualPrice, string OrderId)> ExecuteBuyOrderWithActualPriceAsync(
            OKXRestClient client, string symbol, decimal investmentAmountEUR)
        {
            try
            {
                Log.Information($"🛒 Führe Buy-Order aus: {symbol} mit {investmentAmountEUR:F2} EUR");

                // 1. Buy Order ausführen (Ihre bestehende Logik)
                var buyResponse = await client.UnifiedApi.Trading.PlaceOrderAsync(
                    symbol,
                    OrderSide.Buy,
                    OrderType.Market,
                    tradeMode: TradeMode.Cash,
                    quantity: investmentAmountEUR);

                if (!buyResponse.Success)
                {
                    Log.Error($"❌ Buy-Order fehlgeschlagen: {buyResponse.Error}");
                    return (false, 0, null);
                }

                var orderId = buyResponse.Data?.OrderId;
                Log.Information($"✅ Order platziert - Order ID: {orderId}");

                // 2. ✅ METHODE 1: Tatsächlichen Preis aus Order Details holen
                var actualPrice = await GetActualPriceFromOrderAsync(client, $"{orderId}");

                if (actualPrice > 0)
                {
                    Log.Information($"💰 Tatsächlicher Kaufpreis: {actualPrice:F6} EUR (via Order Details)");
                    return (true, actualPrice, $"{orderId}");
                }
                else
                {
                    Log.Warning($"⚠️ Konnte tatsächlichen Preis nicht ermitteln für Order {orderId}");
                    return (true, 0, $"{orderId}"); // Order war erfolgreich, aber Preis unbekannt
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"❌ Fehler bei Buy-Order für {symbol}");
                return (false, 0, string.Empty);
            }
        }

        /// <summary>
        /// ✅ METHODE 1: Holt den tatsächlichen Preis aus den Order-Details
        /// </summary>
        public static async Task<decimal> GetActualPriceFromOrderAsync(OKXRestClient client, string orderId)
        {
            try
            {
                // Kurz warten bis Order verarbeitet ist
                await Task.Delay(1500);

                var orderDetails = await client.UnifiedApi.Trading.GetOrderDetailsAsync($"{orderId}");
                if (orderDetails.Success && orderDetails.Data != null)
                {
                    var order = orderDetails.Data;

                    Log.Debug($"📋 Order Status: {order.OrderState}");
                    Log.Debug($"📋 Filled Quantity: {order.QuantityFilled}");
                    Log.Debug($"📋 Average Fill Price: {order.Price}");

                    // ✅ Average Fill Price ist der tatsächliche Kaufpreis
                    if (order.Price.HasValue && order.Price > 0)
                    {
                        return (decimal)order.FillPrice;
                    }

                    // Falls AverageFillPrice nicht verfügbar, aber Order gefüllt ist
                    if (order.OrderState == OrderStatus.Filled && order.QuantityFilled.HasValue && order.QuantityFilled > 0)
                    {
                        Log.Warning($"⚠️ AverageFillPrice nicht verfügbar, aber Order ist gefüllt");
                        // Hier könnten Sie einen Fallback implementieren
                    }
                }
                else
                {
                    Log.Warning($"⚠️ Konnte Order Details nicht abrufen: {orderDetails.Error}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"⚠️ Fehler beim Abrufen der Order Details: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Retry-Version falls der erste Versuch fehlschlägt
        /// </summary>
        public static async Task<decimal> GetActualPriceFromOrderWithRetryAsync(OKXRestClient client, string orderId, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                var price = await GetActualPriceFromOrderAsync(client, orderId);

                if (price > 0)
                {
                    Log.Debug($"✅ Preis erfolgreich ermittelt nach {attempt} Versuchen");
                    return price;
                }

                if (attempt < maxRetries)
                {
                    Log.Debug($"🔄 Preis-Ermittlung Versuch {attempt} fehlgeschlagen, versuche erneut...");
                    await Task.Delay(2000 * attempt); // Längere Wartezeit bei jedem Versuch
                }
            }

            Log.Warning($"❌ Konnte Preis nach {maxRetries} Versuchen nicht ermitteln");
            return 0;
        }
    }
}