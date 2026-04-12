
namespace TradeFlowGuardian.Domain.Entities;

public class Position
{
    public string Instrument { get; set; } = string.Empty;
    public long Units { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal UnrealizedPL { get; set; }
    public string Side { get; set; } = string.Empty; // "long", "short", "flat"
    
    // OANDA-specific details for advanced use cases
    public long LongUnits { get; set; }
    public long ShortUnits { get; set; }
    public decimal LongAveragePrice { get; set; }
    public decimal ShortAveragePrice { get; set; }
    
    public bool IsLong => Units > 0;
    public bool IsShort => Units < 0;
    public bool IsFlat => Units == 0;
    
    public override string ToString() =>
        $"{Instrument}: {Units} units @ {AveragePrice:F5} (P&L: {UnrealizedPL:F2})";
}
