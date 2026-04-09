using FxRatesApi.Api.Domain.Utilities;

namespace FxRatesApi.Api.Tests.Domain.Utilities;

public class CurrencyCodeUtilityTests
{
    [Fact]
    public void Normalize_ReturnsUppercase_ForSupportedCurrency()
    {
        var result = CurrencyCodeUtility.Normalize("usd");

        Assert.Equal("USD", result);
    }

    [Fact]
    public void Normalize_ThrowsArgumentException_ForUnsupportedCurrency()
    {
        var exception = Assert.Throws<ArgumentException>(() => CurrencyCodeUtility.Normalize("ABC", "baseCurrency"));

        Assert.Equal("baseCurrency", exception.ParamName);
        Assert.Contains("not supported", exception.Message);
    }
}