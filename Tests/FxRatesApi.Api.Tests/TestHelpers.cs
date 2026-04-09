using FxRatesApi.Api.Application.Events;
using FxRatesApi.Api.Application.Services;
using FxRatesApi.Api.Domain.Models;
using FxRatesApi.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FxRatesApi.Api.Tests;

internal static class TestHelpers
{
    public static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}

internal sealed class FakeRateEventPublisher : IRateEventPublisher
{
    public List<ExchangeRateCreatedEvent> PublishedEvents { get; } = [];

    public ValueTask PublishAsync(ExchangeRateCreatedEvent exchangeRateEvent, CancellationToken cancellationToken = default)
    {
        PublishedEvents.Add(exchangeRateEvent);
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeExchangeRateResolver : IExchangeRateResolver
{
    public string? LastBaseCurrency { get; private set; }
    public string? LastQuoteCurrency { get; private set; }
    public Func<string, string, ExchangeRateLookupResult>? OnResolve { get; set; }

    public Task<ExchangeRateLookupResult> ResolveAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default)
    {
        LastBaseCurrency = baseCurrency;
        LastQuoteCurrency = quoteCurrency;

        var result = OnResolve?.Invoke(baseCurrency, quoteCurrency)
            ?? ExchangeRateLookupResult.FromDatabase(new ExchangeRate
            {
                BaseCurrency = baseCurrency,
                QuoteCurrency = quoteCurrency,
                Bid = 1.0m,
                Ask = 1.1m,
                Provider = "ResolverStub"
            });

        return Task.FromResult(result);
    }
}

internal sealed class FakeExchangeRateProvider : IExchangeRateProvider
{
    public int CallCount { get; private set; }
    public Func<string, string, ExchangeRate?>? OnGetExchangeRate { get; set; }
    public Func<string, string, Exception?>? OnThrow { get; set; }

    public Task<ExchangeRate?> GetExchangeRateAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default)
    {
        CallCount++;

        var exception = OnThrow?.Invoke(baseCurrency, quoteCurrency);
        if (exception is not null)
        {
            throw exception;
        }

        return Task.FromResult(OnGetExchangeRate?.Invoke(baseCurrency, quoteCurrency));
    }
}