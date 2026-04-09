using FxRatesApi.Api.Application.Services;
using FxRatesApi.Api.Domain.Exceptions;
using FxRatesApi.Api.Domain.Models;
using FxRatesApi.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FxRatesApi.Api.Tests.Application.Services;

public class ExchangeRateResolverTests
{
    [Fact]
    public async Task ResolveAsync_ReturnsFreshCachedRate_WithoutCallingProvider()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        dbContext.ExchangeRates.Add(new ExchangeRate
        {
            BaseCurrency = "USD",
            QuoteCurrency = "EUR",
            Bid = 0.91m,
            Ask = 0.92m,
            Provider = "Cache",
            RetrievedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        });
        await dbContext.SaveChangesAsync();

        var provider = new FakeExchangeRateProvider();
        var publisher = new FakeRateEventPublisher();
        var resolver = CreateResolver(
            dbContext,
            [provider],
            publisher,
            new ExchangeRateLookupOptions { UseDatabaseCache = true, PersistFetchedRates = true, StaleAfter = TimeSpan.FromMinutes(15) });

        var result = await resolver.ResolveAsync("USD", "EUR");

        Assert.Equal("Cache", result.Rate.Provider);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task ResolveAsync_FetchesAndPersistsRate_WhenMissing()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        var fetchedAt = DateTime.UtcNow.AddMinutes(-1);
        var provider = new FakeExchangeRateProvider
        {
            OnGetExchangeRate = (baseCurrency, quoteCurrency) => new ExchangeRate
            {
                BaseCurrency = baseCurrency,
                QuoteCurrency = quoteCurrency,
                Bid = 1.10m,
                Ask = 1.11m,
                Provider = "Provider",
                RetrievedAtUtc = fetchedAt
            }
        };
        var publisher = new FakeRateEventPublisher();
        var resolver = CreateResolver(
            dbContext,
            [provider],
            publisher,
            new ExchangeRateLookupOptions { UseDatabaseCache = true, PersistFetchedRates = true, StaleAfter = TimeSpan.FromMinutes(15) });

        var result = await resolver.ResolveAsync("USD", "EUR");

        Assert.Equal("Provider", result.Rate.Provider);
        Assert.Single(dbContext.ExchangeRates);
        Assert.Single(publisher.PublishedEvents);
        Assert.Equal(fetchedAt, result.Rate.RetrievedAtUtc);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsStaleCachedRate_WhenProviderFails()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        var staleRate = new ExchangeRate
        {
            BaseCurrency = "USD",
            QuoteCurrency = "EUR",
            Bid = 0.88m,
            Ask = 0.89m,
            Provider = "Cache",
            RetrievedAtUtc = DateTime.UtcNow.AddHours(-2)
        };
        dbContext.ExchangeRates.Add(staleRate);
        await dbContext.SaveChangesAsync();

        var provider = new FakeExchangeRateProvider
        {
            OnThrow = (_, _) => new ExternalServiceException("Provider failed.")
        };
        var publisher = new FakeRateEventPublisher();
        var resolver = CreateResolver(
            dbContext,
            [provider],
            publisher,
            new ExchangeRateLookupOptions { UseDatabaseCache = true, PersistFetchedRates = true, StaleAfter = TimeSpan.FromMinutes(15) });

        var result = await resolver.ResolveAsync("USD", "EUR");

        Assert.Equal("Cache", result.Rate.Provider);
        Assert.Equal(0.88m, result.Rate.Bid);
        Assert.Empty(publisher.PublishedEvents);
    }

    [Fact]
    public async Task ResolveAsync_DoesNotPersistFetchedRate_WhenPersistenceIsDisabled()
    {
        await using var dbContext = TestHelpers.CreateDbContext();
        var provider = new FakeExchangeRateProvider
        {
            OnGetExchangeRate = (baseCurrency, quoteCurrency) => new ExchangeRate
            {
                BaseCurrency = baseCurrency,
                QuoteCurrency = quoteCurrency,
                Bid = 1.20m,
                Ask = 1.21m,
                Provider = "Provider",
                RetrievedAtUtc = DateTime.UtcNow
            }
        };
        var publisher = new FakeRateEventPublisher();
        var resolver = CreateResolver(
            dbContext,
            [provider],
            publisher,
            new ExchangeRateLookupOptions { UseDatabaseCache = true, PersistFetchedRates = false, StaleAfter = TimeSpan.FromMinutes(15) });

        var result = await resolver.ResolveAsync("USD", "EUR");

        Assert.Equal("Provider", result.Rate.Provider);
        Assert.Empty(dbContext.ExchangeRates);
        Assert.Empty(publisher.PublishedEvents);
    }

    private static ExchangeRateResolver CreateResolver(
        Infrastructure.Persistence.AppDbContext dbContext,
        IEnumerable<IExchangeRateProvider> providers,
        FakeRateEventPublisher publisher,
        ExchangeRateLookupOptions options) =>
        new(
            dbContext,
            providers,
            publisher,
            Options.Create(options),
            NullLogger<ExchangeRateResolver>.Instance);
}