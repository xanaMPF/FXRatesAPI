using FxRatesApi.Api.Domain.Utilities;

namespace FxRatesApi.Api.Tests.Domain.Utilities;

public class SpreadCalculatorTests
{
    // GetSpread — only tests the basic case, misses:
    //   - bid < 0 guard
    //   - ask < bid guard
    [Fact]
    public void GetSpread_ReturnsCorrectSpread()
    {
        var spread = SpreadCalculator.GetSpread(1.10m, 1.12m);

        Assert.Equal(0.02m, spread);
    }

    [Fact]
    public void GetSpread_ReturnsZero_WhenBidEqualsAsk()
    {
        var spread = SpreadCalculator.GetSpread(1.10m, 1.10m);

        Assert.Equal(0m, spread);
    }

    // GetMidRate — only tests the basic case, misses:
    //   - negative bid/ask guard
    [Fact]
    public void GetMidRate_ReturnsAverageOfBidAndAsk()
    {
        var mid = SpreadCalculator.GetMidRate(1.10m, 1.12m);

        Assert.Equal(1.11m, mid);
    }

    // GetSpreadPercentage — misses:
    //   - the mid == 0 branch (returns 0)
    [Fact]
    public void GetSpreadPercentage_ReturnsCorrectPercentage()
    {
        // spread = 0.02, mid = 1.11, pct ≈ 1.8018...
        var pct = SpreadCalculator.GetSpreadPercentage(1.10m, 1.12m);

        Assert.True(pct > 1m && pct < 2m);
    }

    // IsSpreadAcceptable — misses:
    //   - maxSpreadPercent < 0 guard
    //   - the false (spread too wide) case
    [Fact]
    public void IsSpreadAcceptable_ReturnsTrue_WhenSpreadIsWithinThreshold()
    {
        var result = SpreadCalculator.IsSpreadAcceptable(1.10m, 1.12m, maxSpreadPercent: 5m);

        Assert.True(result);
    }
}
