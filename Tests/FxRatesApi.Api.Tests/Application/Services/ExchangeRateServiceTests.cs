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
        Assert.Single(dbContext.ExchangeRates);
        Assert.Single(publisher.PublishedEvents);
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
        var resolver = new FakeExchangeRateResolver();
        var publisher = new FakeRateEventPublisher();
        var service = new ExchangeRateService(dbContext, resolver, publisher);

        var result = await service.UpdateAsync("USD", "EUR", new UpdateExchangeRateRequest
        {
            Bid = 1.0m,
            Ask = 1.1m,
            Provider = "Manual"
        });

        Assert.Null(result);
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

        var resolver = new FakeExchangeRateResolver();
        var publisher = new FakeRateEventPublisher();
        var service = new ExchangeRateService(dbContext, resolver, publisher);

        var deleted = await service.DeleteAsync("USD", "EUR");

        Assert.True(deleted);
        Assert.Empty(dbContext.ExchangeRates);
    }
}