using FxRatesApi.Api.Domain.Constants;

namespace FxRatesApi.Api.Infrastructure.Configuration;

public class AlphaVantageOptions
{
    public const string SectionName = "AlphaVantage";

    public string BaseUrl { get; set; } = AlphaVantageConstants.DefaultBaseUrl;
    public string ApiKey { get; set; } = string.Empty;
}
