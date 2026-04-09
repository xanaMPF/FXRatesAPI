using System.Globalization;
using FxRatesApi.Api.Domain.Constants;
using FxRatesApi.Api.Domain.Exceptions;
using FxRatesApi.Api.Domain.Models;
using FxRatesApi.Api.Domain.Utilities;

namespace FxRatesApi.Api.Infrastructure.Providers.AlphaVantage;

public static class AlphaVantageExchangeRateMapper
{
    public static ExchangeRate? Map(AlphaVantageExchangeRateResponse response, string baseCurrency, string quoteCurrency)
    {
        if (!string.IsNullOrWhiteSpace(response.Note))
        {
            throw new ExternalServiceException(response.Note);
        }

        if (!string.IsNullOrWhiteSpace(response.ErrorMessage))
        {
            throw new ExternalServiceException(response.ErrorMessage);
        }

        var ratePayload = response.RealtimeCurrencyExchangeRate;
        if (ratePayload is null)
        {
            return null;
        }

        return new ExchangeRate
        {
            BaseCurrency = CurrencyCodeUtility.Normalize(baseCurrency, nameof(baseCurrency)),
            QuoteCurrency = CurrencyCodeUtility.Normalize(quoteCurrency, nameof(quoteCurrency)),
            Bid = Parse(ratePayload.BidPrice, AlphaVantageConstants.InvalidBidPriceMessage),
            Ask = Parse(ratePayload.AskPrice, AlphaVantageConstants.InvalidAskPriceMessage),
            Provider = ExchangeRateProviders.AlphaVantage,
            RetrievedAtUtc = ParseRetrievedAtUtc(ratePayload.LastRefreshed)
        };
    }

    private static decimal Parse(string? rawValue, string errorMessage)
    {
        if (!decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue))
        {
            throw new ExternalServiceException(errorMessage);
        }

        return parsedValue;
    }

    private static DateTime ParseRetrievedAtUtc(string? rawValue) =>
        DateTime.TryParse(
            rawValue,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsedDate)
            ? parsedDate
            : DateTime.UtcNow;
}
