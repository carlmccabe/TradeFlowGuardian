using System;
using System.Text.Json;

namespace TradeFlowGuardian.Infrastructure.Services.Oanda.StreamingModels
{
    // Minimal representation of transaction stream events we care about.
    public class TransactionEvent
    {
        public string Type { get; set; } = "";
        public string? OrderID { get; set; }
        public string? Instrument { get; set; }
        public decimal? Units { get; set; }
        public decimal? Price { get; set; }
        public DateTime Time { get; set; }

        public static TransactionEvent? FromJson(JsonElement el)
        {
            if (!el.TryGetProperty("type", out var typeEl)) return null;
            var type = typeEl.GetString() ?? string.Empty;

            var e = new TransactionEvent { Type = type };

            if (el.TryGetProperty("orderID", out var orderIdEl))
                e.OrderID = orderIdEl.GetString();

            if (el.TryGetProperty("instrument", out var instEl))
                e.Instrument = instEl.GetString();

            if (el.TryGetProperty("units", out var unitsEl))
            {
                if (unitsEl.ValueKind == JsonValueKind.String && decimal.TryParse(unitsEl.GetString(), out var u))
                    e.Units = u;
                else if (unitsEl.ValueKind == JsonValueKind.Number && unitsEl.TryGetDecimal(out var u2))
                    e.Units = u2;
            }

            if (el.TryGetProperty("price", out var priceEl))
            {
                if (priceEl.ValueKind == JsonValueKind.String && decimal.TryParse(priceEl.GetString(), out var p))
                    e.Price = p;
                else if (priceEl.ValueKind == JsonValueKind.Number && priceEl.TryGetDecimal(out var p2))
                    e.Price = p2;
            }

            if (el.TryGetProperty("time", out var timeEl) && timeEl.ValueKind == JsonValueKind.String && DateTime.TryParse(timeEl.GetString(), out var dt))
                e.Time = dt;
            else
                e.Time = DateTime.UtcNow;

            return e;
        }
    }
}