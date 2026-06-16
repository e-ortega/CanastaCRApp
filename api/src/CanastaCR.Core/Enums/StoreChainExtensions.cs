namespace CanastaCR.Core.Enums;

public static class StoreChainExtensions
{
    /// <summary>
    /// Human-readable chain name, used as the display "store" label for chain-level prices
    /// (no specific location — see PriceReport.Chain). Deliberately not an override of
    /// ToString(): EF Core cannot translate arbitrary C# methods inside a LINQ query that
    /// becomes SQL, so this must only ever be called after results are materialized.
    /// </summary>
    public static string GetDisplayName(this StoreChain chain) => chain switch
    {
        StoreChain.AutoMercado => "AutoMercado",
        StoreChain.MasXMenos => "Más x Menos",
        StoreChain.MaxiPali => "MaxiPalí",
        StoreChain.MegaSuper => "MegaSuper",
        StoreChain.PriceSmart => "PriceSmart",
        StoreChain.Walmart => "Walmart",
        _ => "Otro"
    };
}
