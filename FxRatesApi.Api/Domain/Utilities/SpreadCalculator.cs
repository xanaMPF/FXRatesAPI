namespace FxRatesApi.Api.Domain.Utilities;

public static class SpreadCalculator
{
    /// <summary>
    /// Returns the absolute spread between ask and bid.
    /// </summary>
    public static decimal GetSpread(decimal bid, decimal ask)
    {
        if (bid < 0 || ask < 0)
            throw new ArgumentException("Bid and ask must be non-negative.");

        if (ask < bid)
            throw new ArgumentException("Ask must be greater than or equal to bid.");

        return ask - bid;
    }

    /// <summary>
    /// Returns the mid-rate between bid and ask.
    /// </summary>
    public static decimal GetMidRate(decimal bid, decimal ask)
    {
        if (bid < 0 || ask < 0)
            throw new ArgumentException("Bid and ask must be non-negative.");

        return (bid + ask) / 2m;
    }

    /// <summary>
    /// Returns the spread as a percentage of the mid-rate.
    /// Returns 0 when mid-rate is zero to avoid division by zero.
    /// </summary>
    public static decimal GetSpreadPercentage(decimal bid, decimal ask)
    {
        var spread = GetSpread(bid, ask);
        var mid = GetMidRate(bid, ask);

        if (mid == 0m)
            return 0m;

        return spread / mid * 100m;
    }

    /// <summary>
    /// Returns true when the spread percentage is within the given threshold.
    /// </summary>
    public static bool IsSpreadAcceptable(decimal bid, decimal ask, decimal maxSpreadPercent)
    {
        if (maxSpreadPercent < 0)
            throw new ArgumentException("Max spread percent must be non-negative.");

        return GetSpreadPercentage(bid, ask) <= maxSpreadPercent;
    }

    /// <summary>
    /// Classifies the spread as Tight, Normal, or Wide based on percentage thresholds.
    /// </summary>
    public static string ClassifySpread(decimal bid, decimal ask)
    {
        var pct = GetSpreadPercentage(bid, ask);

        if (pct < 0.5m)
            return "Tight";

        if (pct < 2.0m)
            return "Normal";

        return "Wide";
    }
}
