using System.Net;
using System.Text;
using FxRatesApi.Api.Domain.Exceptions;
using FxRatesApi.Api.Infrastructure.Configuration;
using FxRatesApi.Api.Infrastructure.Providers.AlphaVantage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace FxRatesApi.Api.Tests.Infrastructure.Providers.AlphaVantage;

public class AlphaVantageServiceTests
{
    private static AlphaVantageService CreateService(
        HttpMessageHandler handler,
        string apiKey = "REAL_KEY",
        string baseUrl = "https://www.alphavantage.co/query",
        TimeProvider? timeProvider = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = null };
        var options = Options.Create(new AlphaVantageOptions { ApiKey = apiKey, BaseUrl = baseUrl });
        return new AlphaVantageService(httpClient, options, NullLogger<AlphaVantageService>.Instance, timeProvider ?? TimeProvider.System);
    }

    private static HttpMessageHandler JsonHandler(string json, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new FakeHttpMessageHandler(statusCode, json);

    private static string ValidJson() => """
        {
          "Realtime Currency Exchange Rate": {
            "6. Last Refreshed": "2026-01-01 12:00:00",
            "8. Bid Price": "0.92000",
            "9. Ask Price": "0.93000"
          }
        }
        """;

    [Fact]
    public async Task GetExchangeRateAsync_ReturnsRate_WhenResponseIsValid()
    {
        var service = CreateService(JsonHandler(ValidJson()));

        var result = await service.GetExchangeRateAsync("USD", "EUR");

        Assert.NotNull(result);
        Assert.Equal("USD", result.BaseCurrency);
        Assert.Equal("EUR", result.QuoteCurrency);
        Assert.Equal(0.92m, result.Bid);
        Assert.Equal(0.93m, result.Ask);
    }

    [Fact]
    public async Task GetExchangeRateAsync_ReturnsNull_WhenPayloadIsMissing()
    {
        var service = CreateService(JsonHandler("""{ "Note": null }"""));

        var result = await service.GetExchangeRateAsync("USD", "EUR");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_ThrowsExternalServiceException_WhenApiKeyIsMissing()
    {
        var service = CreateService(JsonHandler(ValidJson()), apiKey: "");

        await Assert.ThrowsAsync<ExternalServiceException>(() =>
            service.GetExchangeRateAsync("USD", "EUR"));
    }

    [Fact]
    public async Task GetExchangeRateAsync_ThrowsExternalServiceException_WhenApiKeyIsPlaceholder()
    {
        var service = CreateService(JsonHandler(ValidJson()), apiKey: "YOUR_API_KEY");

        await Assert.ThrowsAsync<ExternalServiceException>(() =>
            service.GetExchangeRateAsync("USD", "EUR"));
    }

    [Fact]
    public async Task GetExchangeRateAsync_ThrowsExternalServiceException_WhenApiKeyIsOnlyWhitespace()
    {
        var service = CreateService(JsonHandler(ValidJson()), apiKey: "   ");

        await Assert.ThrowsAsync<ExternalServiceException>(() =>
            service.GetExchangeRateAsync("USD", "EUR"));
    }

    [Fact]
    public async Task GetExchangeRateAsync_ThrowsHttpRequestException_WhenResponseIsUnsuccessful()
    {
        var service = CreateService(JsonHandler("{}", HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetExchangeRateAsync("USD", "EUR"));
    }

    [Fact]
    public async Task GetExchangeRateAsync_ThrowsExternalServiceException_WhenNoteIsPresent()
    {
        var service = CreateService(JsonHandler("""{ "Note": "API rate limit reached." }"""));

        await Assert.ThrowsAsync<ExternalServiceException>(() =>
            service.GetExchangeRateAsync("USD", "EUR"));
    }

    [Fact]
    public async Task GetExchangeRateAsync_ThrowsExternalServiceException_WhenErrorMessageIsPresent()
    {
        var service = CreateService(JsonHandler("""{ "Error Message": "Invalid API call." }"""));

        await Assert.ThrowsAsync<ExternalServiceException>(() =>
            service.GetExchangeRateAsync("USD", "EUR"));
    }

    [Fact]
    public async Task GetExchangeRateAsync_ThrowsExternalServiceException_WhenJsonIsEmpty()
    {
        // JsonSerializer.DeserializeAsync returns null for "null" JSON → triggers EmptyResponseMessage
        var service = CreateService(JsonHandler("null"));

        await Assert.ThrowsAsync<ExternalServiceException>(() =>
            service.GetExchangeRateAsync("USD", "EUR"));
    }
}

internal sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
