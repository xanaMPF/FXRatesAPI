using FxRatesApi.Api.Application.Dtos;
using FxRatesApi.Api.Application.Services;
using FxRatesApi.Api.Domain.Models;

namespace FxRatesApi.Api.Tests.Application.Services;

public class ExchangeRateServiceTests
{
    [Fact]
    public async Task UpsertAsync_CreatesNewRate_AndPublishesCreateEvent()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        var resolver = new FakeExchangeRateResolver();
        var publisher = new FakeRateEventPublisher();
        var service = new ExchangeRateService(dbContext, resolver, publisher);

        var result = await service.UpsertAsync(new UpsertExchangeRateRequest
        {
            BaseCurrency = "usd",
            QuoteCurrency = "eur",
            Bid = 0.92m,
            Ask = 0.93m,
            Provider = "Manual"
        });

        Assert.True(result.Created);
        Assert.Equal("USD", result.Rate.BaseCurrency);
        Assert.Equal("EUR", result.Rate.QuoteCurrency);
        Assert.Equal(0.92m, result.Rate.Bid);
        Assert.Equal(0.93m, result.Rate.Ask);
        Assert.Equal("Manual", result.Rate.Provider);
        Assert.Single(dbContext.ExchangeRates);
        Assert.Single(publisher.PublishedEvents);
    }

    [Fact]
    public async Task UpsertAsync_PublishesEvent_WithCorrectValues()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        var publisher = new FakeRateEventPublisher();
        var service = new ExchangeRateService(dbContext, new FakeExchangeRateResolver(), publisher);

        await service.UpsertAsync(new UpsertExchangeRateRequest
        {
            BaseCurrency = "USD",
            QuoteCurrency = "GBP",
            Bid = 0.78m,
            Ask = 0.79m,
            Provider = "Manual"
        });

        var evt = Assert.Single(publisher.PublishedEvents);
        Assert.Equal("USD", evt.BaseCurrency);
        Assert.Equal("GBP", evt.QuoteCurrency);
        Assert.Equal(0.78m, evt.Bid);
        Assert.Equal(0.79m, evt.Ask);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingRate_WithoutPublishingCreateEvent()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        dbContext.ExchangeRates.Add(new ExchangeRate
        {
            BaseCurrency = "USD",
            QuoteCurrency = "EUR",
            Bid = 0.90m,
            Ask = 0.91m,
            Provider = "Seed"
        });
        await dbContext.SaveChangesAsync();

        var resolver = new FakeExchangeRateResolver();
        var publisher = new FakeRateEventPublisher();
        var service = new ExchangeRateService(dbContext, resolver, publisher);

        var result = await service.UpsertAsync(new UpsertExchangeRateRequest
        {
            BaseCurrency = "USD",
            QuoteCurrency = "EUR",
            Bid = 0.95m,
            Ask = 0.96m,
            Provider = "Manual"
        });

        Assert.False(result.Created);
        Assert.Equal(0.95m, result.Rate.Bid);
        Assert.Equal(0.96m, result.Rate.Ask);
        Assert.Single(dbContext.ExchangeRates);
        Assert.Empty(publisher.PublishedEvents);
    }

    [Fact]
    public async Task UpsertAsync_UsesManualProvider_WhenProviderIsNull()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        var service = new ExchangeRateService(dbContext, new FakeExchangeRateResolver(), new FakeRateEventPublisher());

        var result = await service.UpsertAsync(new UpsertExchangeRateRequest
        {
            BaseCurrency = "USD",
            QuoteCurrency = "EUR",
            Bid = 1.0m,
            Ask = 1.1m,
            Provider = null
        });

        Assert.Equal("Manual", result.Rate.Provider);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsRates_OrderedByBaseThenQuote()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        dbContext.ExchangeRates.AddRange(
            new ExchangeRate { BaseCurrency = "USD", QuoteCurrency = "JPY", Bid = 1m, Ask = 1m, Provider = "X" },
            new ExchangeRate { BaseCurrency = "EUR", QuoteCurrency = "USD", Bid = 1m, Ask = 1m, Provider = "X" },
            new ExchangeRate { BaseCurrency = "USD", QuoteCurrency = "EUR", Bid = 1m, Ask = 1m, Provider = "X" }
        );
        await dbContext.SaveChangesAsync();

        var service = new ExchangeRateService(dbContext, new FakeExchangeRateResolver(), new FakeRateEventPublisher());
        var result = await service.GetAllAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal("EUR", result[0].BaseCurrency);
        Assert.Equal("USD", result[1].BaseCurrency);
        Assert.Equal("EUR", result[1].QuoteCurrency);
        Assert.Equal("USD", result[2].BaseCurrency);
        Assert.Equal("JPY", result[2].QuoteCurrency);
    }

    [Fact]
    public async Task GetCurrentAsync_NormalizesPair_BeforeDelegatingToResolver()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        var resolver = new FakeExchangeRateResolver();
        var publisher = new FakeRateEventPublisher();
        var service = new ExchangeRateService(dbContext, resolver, publisher);

        _ = await service.GetCurrentAsync("usd", "eur");

        Assert.Equal("USD", resolver.LastBaseCurrency);
        Assert.Equal("EUR", resolver.LastQuoteCurrency);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenPairDoesNotExist()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        var service = new ExchangeRateService(dbContext, new FakeExchangeRateResolver(), new FakeRateEventPublisher());

        var result = await service.UpdateAsync("USD", "EUR", new UpdateExchangeRateRequest
        {
            Bid = 1.0m,
            Ask = 1.1m,
            Provider = "Manual"
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesBidAskAndProvider_WhenPairExists()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        dbContext.ExchangeRates.Add(new ExchangeRate
        {
            BaseCurrency = "USD",
            QuoteCurrency = "EUR",
            Bid = 0.90m,
            Ask = 0.91m,
            Provider = "Old"
        });
        await dbContext.SaveChangesAsync();

        var service = new ExchangeRateService(dbContext, new FakeExchangeRateResolver(), new FakeRateEventPublisher());

        var result = await service.UpdateAsync("USD", "EUR", new UpdateExchangeRateRequest
        {
            Bid = 1.05m,
            Ask = 1.06m,
            Provider = "NewProvider"
        });

        Assert.NotNull(result);
        Assert.Equal(1.05m, result.Bid);
        Assert.Equal(1.06m, result.Ask);
        Assert.Equal("NewProvider", result.Provider);
    }

    [Fact]
    public async Task UpdateAsync_KeepsExistingProvider_WhenNullProviderRequested()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        dbContext.ExchangeRates.Add(new ExchangeRate
        {
            BaseCurrency = "USD",
            QuoteCurrency = "EUR",
            Bid = 0.90m,
            Ask = 0.91m,
            Provider = "OriginalProvider"
        });
        await dbContext.SaveChangesAsync();

        var service = new ExchangeRateService(dbContext, new FakeExchangeRateResolver(), new FakeRateEventPublisher());

        var result = await service.UpdateAsync("USD", "EUR", new UpdateExchangeRateRequest
        {
            Bid = 1.0m,
            Ask = 1.1m,
            Provider = null
        });

        Assert.Equal("OriginalProvider", result!.Provider);
    }

    [Fact]
    public async Task DeleteAsync_RemovesExistingPair()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        dbContext.ExchangeRates.Add(new ExchangeRate
        {
            BaseCurrency = "USD",
            QuoteCurrency = "EUR",
            Bid = 0.90m,
            Ask = 0.91m,
            Provider = "Seed"
        });
        await dbContext.SaveChangesAsync();

        var service = new ExchangeRateService(dbContext, new FakeExchangeRateResolver(), new FakeRateEventPublisher());

        var deleted = await service.DeleteAsync("USD", "EUR");

        Assert.True(deleted);
        Assert.Empty(dbContext.ExchangeRates);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenPairDoesNotExist()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        var service = new ExchangeRateService(dbContext, new FakeExchangeRateResolver(), new FakeRateEventPublisher());

        var deleted = await service.DeleteAsync("USD", "EUR");

        Assert.False(deleted);
    }
}