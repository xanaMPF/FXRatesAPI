namespace FxRatesApi.Api.Domain.Constants;

public static class AlphaVantageConstants
{
    public const string DefaultBaseUrl = "https://www.alphavantage.co/query";
    public const string CurrencyExchangeRateFunction = "CURRENCY_EXCHANGE_RATE";
    public const string ApiKeyEnvironmentVariable = "ALPHAVANTAGE_API_KEY";
    public const string ApiKeyPlaceholderToken = "YOUR_API_KEY";
    public const string MissingApiKeyMessage = "AlphaVantage API key is not configured. Set AlphaVantage:ApiKey in appsettings or use the ALPHAVANTAGE_API_KEY environment variable.";
    public const string EmptyResponseMessage = "Alpha Vantage returned an empty response.";
    public const string InvalidBidPriceMessage = "Alpha Vantage returned an invalid bid price.";
    public const string InvalidAskPriceMessage = "Alpha Vantage returned an invalid ask price.";
}

public static class AlphaVantageJsonFields
{
    public const string RealtimeCurrencyExchangeRate = "Realtime Currency Exchange Rate";
    public const string Note = "Note";
    public const string ErrorMessage = "Error Message";
    public const string LastRefreshed = "6. Last Refreshed";
    public const string BidPrice = "8. Bid Price";
    public const string AskPrice = "9. Ask Price";
}
