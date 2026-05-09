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
    public void Normalize_TrimsWhitespace_BeforeNormalizing()
    {
        var result = CurrencyCodeUtility.Normalize(" usd ");

        Assert.Equal("USD", result);
    }

    [Fact]
    public void Normalize_ThrowsArgumentException_ForUnsupportedCurrency()
    {
        var exception = Assert.Throws<ArgumentException>(() => CurrencyCodeUtility.Normalize("ABC", "baseCurrency"));

        Assert.Equal("baseCurrency", exception.ParamName);
        Assert.Contains("not supported", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_ThrowsArgumentException_ForNullOrWhitespace(string? value)
    {
        var exception = Assert.Throws<ArgumentException>(() => CurrencyCodeUtility.Normalize(value!, "p"));

        Assert.Equal("p", exception.ParamName);
        Assert.Contains("3-letter ISO code", exception.Message);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    public void Normalize_ThrowsArgumentException_ForWrongLength(string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => CurrencyCodeUtility.Normalize(value, "p"));

        Assert.Equal("p", exception.ParamName);
        Assert.Contains("3-letter ISO code", exception.Message);
    }

    [Fact]
    public void Normalize_UsesDefaultParamName_WhenNotProvided()
    {
        var exception = Assert.Throws<ArgumentException>(() => CurrencyCodeUtility.Normalize("ABC"));

        Assert.Equal("value", exception.ParamName);
    }
}