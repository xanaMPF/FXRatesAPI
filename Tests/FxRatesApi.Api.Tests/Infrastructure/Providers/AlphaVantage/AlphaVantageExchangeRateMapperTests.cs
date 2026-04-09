using FxRatesApi.Api.Domain.Constants;
using FxRatesApi.Api.Domain.Exceptions;
using FxRatesApi.Api.Infrastructure.Providers.AlphaVantage;

namespace FxRatesApi.Api.Tests.Infrastructure.Providers.AlphaVantage;

public class AlphaVantageExchangeRateMapperTests
{
    private static AlphaVantageExchangeRateResponse ValidResponse(string bid = "0.92000", string ask = "0.93000", string lastRefreshed = "2026-01-01 12:00:00") =>
        new()
        {
            RealtimeCurrencyExchangeRate = new AlphaVantageExchangeRatePayload
            {
                BidPrice = bid,
                AskPrice = ask,
                LastRefreshed = lastRefreshed
            }
        };

    [Fact]
    public void Map_ReturnsExchangeRate_WithCorrectValues()
    {
        var response = ValidResponse(bid: "0.9200", ask: "0.9300", lastRefreshed: "2026-01-01 12:00:00");

        var result = AlphaVantageExchangeRateMapper.Map(response, "USD", "EUR");

        Assert.NotNull(result);
        Assert.Equal("USD", result.BaseCurrency);
        Assert.Equal("EUR", result.QuoteCurrency);
        Assert.Equal(0.92m, result.Bid);
        Assert.Equal(0.93m, result.Ask);
        Assert.Equal(ExchangeRateProviders.AlphaVantage, result.Provider);
    }

    [Fact]
    public void Map_ReturnsNull_WhenPayloadIsAbsent()
    {
        var response = new AlphaVantageExchangeRateResponse();

        var result = AlphaVantageExchangeRateMapper.Map(response, "USD", "EUR");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("This is a note about rate limiting.")]
    [InlineData("   ")] // whitespace only — should NOT throw, treated as empty
    public void Map_ThrowsExternalServiceException_WhenNoteIsPresent(string note)
    {
        // Only non-whitespace notes should throw
        if (string.IsNullOrWhiteSpace(note))
        {
            var responseWithWhitespaceNote = new AlphaVantageExchangeRateResponse
            {
                Note = note,
                RealtimeCurrencyExchangeRate = new AlphaVantageExchangeRatePayload
                {
                    BidPrice = "0.92", AskPrice = "0.93", LastRefreshed = "2026-01-01 12:00:00"
                }
            };
            // whitespace note is ignored, should map normally
            var result = AlphaVantageExchangeRateMapper.Map(responseWithWhitespaceNote, "USD", "EUR");
            Assert.NotNull(result);
        }
        else
        {
            var responseWithNote = new AlphaVantageExchangeRateResponse { Note = note };
            Assert.Throws<ExternalServiceException>(() => AlphaVantageExchangeRateMapper.Map(responseWithNote, "USD", "EUR"));
        }
    }

    [Fact]
    public void Map_ThrowsExternalServiceException_ForNonWhitespaceNote()
    {
        var response = new AlphaVantageExchangeRateResponse
        {
            Note = "Thank you for using Alpha Vantage! Our standard API rate limit is 25 requests per day."
        };

        var ex = Assert.Throws<ExternalServiceException>(() => AlphaVantageExchangeRateMapper.Map(response, "USD", "EUR"));
        Assert.Contains("Alpha Vantage", ex.Message);
    }

    [Fact]
    public void Map_ThrowsExternalServiceException_ForNonWhitespaceErrorMessage()
    {
        var response = new AlphaVantageExchangeRateResponse
        {
            ErrorMessage = "Invalid API call."
        };

        var ex = Assert.Throws<ExternalServiceException>(() => AlphaVantageExchangeRateMapper.Map(response, "USD", "EUR"));
        Assert.Contains("Invalid API call", ex.Message);
    }

    [Fact]
    public void Map_IgnoresWhitespaceErrorMessage_AndMapsNormally()
    {
        var response = new AlphaVantageExchangeRateResponse
        {
            ErrorMessage = "   ",
            RealtimeCurrencyExchangeRate = new AlphaVantageExchangeRatePayload
            {
                BidPrice = "0.92", AskPrice = "0.93", LastRefreshed = "2026-01-01 12:00:00"
            }
        };

        var result = AlphaVantageExchangeRateMapper.Map(response, "USD", "EUR");

        Assert.NotNull(result);
    }

    [Fact]
    public void Map_ThrowsExternalServiceException_WhenBidPriceIsInvalid()
    {
        var response = ValidResponse(bid: "not-a-number");

        var ex = Assert.Throws<ExternalServiceException>(() => AlphaVantageExchangeRateMapper.Map(response, "USD", "EUR"));
        Assert.Equal(AlphaVantageConstants.InvalidBidPriceMessage, ex.Message);
    }

    [Fact]
    public void Map_ThrowsExternalServiceException_WhenAskPriceIsInvalid()
    {
        var response = ValidResponse(ask: "not-a-number");

        var ex = Assert.Throws<ExternalServiceException>(() => AlphaVantageExchangeRateMapper.Map(response, "USD", "EUR"));
        Assert.Equal(AlphaVantageConstants.InvalidAskPriceMessage, ex.Message);
    }

    [Fact]
    public void Map_ThrowsExternalServiceException_WhenBidPriceIsNull()
    {
        var response = new AlphaVantageExchangeRateResponse
        {
            RealtimeCurrencyExchangeRate = new AlphaVantageExchangeRatePayload
            {
                BidPrice = null, AskPrice = "0.93", LastRefreshed = "2026-01-01 12:00:00"
            }
        };

        Assert.Throws<ExternalServiceException>(() => AlphaVantageExchangeRateMapper.Map(response, "USD", "EUR"));
    }

    [Fact]
    public void Map_ThrowsExternalServiceException_WhenAskPriceIsNull()
    {
        var response = new AlphaVantageExchangeRateResponse
        {
            RealtimeCurrencyExchangeRate = new AlphaVantageExchangeRatePayload
            {
                BidPrice = "0.92", AskPrice = null, LastRefreshed = "2026-01-01 12:00:00"
            }
        };

        Assert.Throws<ExternalServiceException>(() => AlphaVantageExchangeRateMapper.Map(response, "USD", "EUR"));
    }

    [Fact]
    public void Map_ParsesLastRefreshed_AsUtc()
    {
        var response = ValidResponse(lastRefreshed: "2026-04-09 10:00:00");

        var result = AlphaVantageExchangeRateMapper.Map(response, "USD", "EUR");

        Assert.NotNull(result);
        Assert.Equal(DateTimeKind.Utc, result.RetrievedAtUtc.Kind);
        Assert.Equal(new DateTime(2026, 4, 9, 10, 0, 0, DateTimeKind.Utc), result.RetrievedAtUtc);
    }

    [Fact]
    public void Map_FallsBackToUtcNow_WhenLastRefreshedIsUnparseable()
    {
        var before = DateTime.UtcNow;
        var response = ValidResponse(lastRefreshed: "not-a-date");

        var result = AlphaVantageExchangeRateMapper.Map(response, "USD", "EUR");
        var after = DateTime.UtcNow;

        Assert.NotNull(result);
        Assert.InRange(result.RetrievedAtUtc, before, after);
    }
}
