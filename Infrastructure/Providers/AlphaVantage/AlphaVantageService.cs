using System.Text.Json;
using FxRatesApi.Api.Application.Services;
using FxRatesApi.Api.Domain.Constants;
using FxRatesApi.Api.Domain.Exceptions;
using FxRatesApi.Api.Domain.Models;
using FxRatesApi.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace FxRatesApi.Api.Infrastructure.Providers.AlphaVantage;


public class AlphaVantageService(
    HttpClient httpClient,
    IOptions<AlphaVantageOptions> options,
    ILogger<AlphaVantageService> logger) : IExchangeRateProvider
{
    private const string FetchRateLogMessage = "Fetching FX rate from Alpha Vantage for {BaseCurrency}/{QuoteCurrency}";

    private readonly HttpClient _httpClient = httpClient;
    private readonly AlphaVantageOptions _options = options.Value;
    private readonly ILogger<AlphaVantageService> _logger = logger;

    public async Task<ExchangeRate?> GetExchangeRateAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default)
    {
        EnsureApiKeyConfigured();

        var requestUrl = BuildExchangeRateUrl(baseCurrency, quoteCurrency);

        _logger.LogInformation(FetchRateLogMessage, baseCurrency, quoteCurrency);

        using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var providerResponse = await DeserializeResponseAsync(response, cancellationToken);
        return AlphaVantageExchangeRateMapper.Map(providerResponse, baseCurrency, quoteCurrency);
    }

    private void EnsureApiKeyConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || _options.ApiKey.Contains(AlphaVantageConstants.ApiKeyPlaceholderToken, StringComparison.OrdinalIgnoreCase))
        {
            throw new ExternalServiceException(AlphaVantageConstants.MissingApiKeyMessage);
        }
    }

    private string BuildExchangeRateUrl(string baseCurrency, string quoteCurrency) =>
        $"{_options.BaseUrl}?function={AlphaVantageConstants.CurrencyExchangeRateFunction}&from_currency={Uri.EscapeDataString(baseCurrency)}&to_currency={Uri.EscapeDataString(quoteCurrency)}&apikey={Uri.EscapeDataString(_options.ApiKey)}";

    private static async Task<AlphaVantageExchangeRateResponse> DeserializeResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return await JsonSerializer.DeserializeAsync<AlphaVantageExchangeRateResponse>(stream, cancellationToken: cancellationToken)
            ?? throw new ExternalServiceException(AlphaVantageConstants.EmptyResponseMessage);
    }
}
