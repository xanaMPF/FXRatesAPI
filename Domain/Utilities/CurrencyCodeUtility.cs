using FxRatesApi.Api.Domain.Constants;

namespace FxRatesApi.Api.Domain.Utilities;

public static class CurrencyCodeUtility
{
    public static string Normalize(string value, string paramName = "value")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Currency code must be a 3-letter ISO code.", paramName);
        }

        var normalizedValue = value.Trim().ToUpperInvariant();

        if (!SupportedCurrencies.Codes.Contains(normalizedValue))
        {
            throw new ArgumentException($"Currency code '{normalizedValue}' is not supported.", paramName);
        }

        return normalizedValue;
    }
}
