using System.Text.Json.Serialization;
using FxRatesApi.Api.Domain.Constants;

namespace FxRatesApi.Api.Infrastructure.Providers.AlphaVantage;

public sealed record AlphaVantageExchangeRateResponse
{
    [JsonPropertyName(AlphaVantageJsonFields.RealtimeCurrencyExchangeRate)]
    public AlphaVantageExchangeRatePayload? RealtimeCurrencyExchangeRate { get; init; }

    [JsonPropertyName(AlphaVantageJsonFields.Note)]
    public string? Note { get; init; }

    [JsonPropertyName(AlphaVantageJsonFields.ErrorMessage)]
    public string? ErrorMessage { get; init; }
}

public sealed record AlphaVantageExchangeRatePayload
{
    [JsonPropertyName(AlphaVantageJsonFields.LastRefreshed)]
    public string? LastRefreshed { get; init; }

    [JsonPropertyName(AlphaVantageJsonFields.BidPrice)]
    public string? BidPrice { get; init; }

    [JsonPropertyName(AlphaVantageJsonFields.AskPrice)]
    public string? AskPrice { get; init; }
}
