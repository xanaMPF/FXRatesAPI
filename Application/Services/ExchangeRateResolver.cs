using FxRatesApi.Api.Application.Events;
using FxRatesApi.Api.Domain.Exceptions;
using FxRatesApi.Api.Domain.Models;
using FxRatesApi.Api.Infrastructure.Configuration;
using FxRatesApi.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FxRatesApi.Api.Application.Services;

public class ExchangeRateResolver(
    AppDbContext dbContext,
    IEnumerable<IExchangeRateProvider> exchangeRateProviders,
    IRateEventPublisher rateEventPublisher,
    IOptions<ExchangeRateLookupOptions> exchangeRateLookupOptions,
    ILogger<ExchangeRateResolver> logger) : IExchangeRateResolver
{
    private const string ProviderFailureLogMessage = "Exchange rate provider {ProviderType} failed for {BaseCurrency}/{QuoteCurrency}";
    private const string StaleFallbackLogMessage = "Returning stale database rate for {BaseCurrency}/{QuoteCurrency} because provider refresh failed.";
    private readonly AppDbContext _dbContext = dbContext;
    private readonly IEnumerable<IExchangeRateProvider> _exchangeRateProviders = exchangeRateProviders;
    private readonly IRateEventPublisher _rateEventPublisher = rateEventPublisher;
    private readonly ExchangeRateLookupOptions _exchangeRateLookupOptions = exchangeRateLookupOptions.Value;
    private readonly ILogger<ExchangeRateResolver> _logger = logger;

    public async Task<ExchangeRateLookupResult> ResolveAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default)
    {
        ExchangeRate? existingRate = null;

        if (_exchangeRateLookupOptions.UseDatabaseCache)
        {
            existingRate = await TryGetFromDatabaseAsync(baseCurrency, quoteCurrency, cancellationToken);
            if (existingRate is not null && !IsStale(existingRate))
            {
                return ExchangeRateLookupResult.FromDatabase(existingRate);
            }
        }

        try
        {
            var fetchedRate = await TryGetFromProvidersAsync(baseCurrency, quoteCurrency, cancellationToken);
            if (fetchedRate is null)
            {
                if (existingRate is not null)
                {
                    return ExchangeRateLookupResult.FromDatabase(existingRate);
                }

                throw new KeyNotFoundException($"Rate for {baseCurrency}/{quoteCurrency} was not found.");
            }

            var currentRate = _exchangeRateLookupOptions.PersistFetchedRates
                ? await UpsertFetchedRateAsync(baseCurrency, quoteCurrency, fetchedRate, cancellationToken)
                : fetchedRate;

            return ExchangeRateLookupResult.FromProvider(currentRate);
        }
        catch (ExternalServiceException) when (existingRate is not null)
        {
            _logger.LogWarning(StaleFallbackLogMessage, baseCurrency, quoteCurrency);
            return ExchangeRateLookupResult.FromDatabase(existingRate);
        }
    }

    private Task<ExchangeRate?> TryGetFromDatabaseAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken) =>
        _dbContext.ExchangeRates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BaseCurrency == baseCurrency && x.QuoteCurrency == quoteCurrency, cancellationToken);

    private async Task<ExchangeRate?> TryGetFromProvidersAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken)
    {
        ExternalServiceException? lastExternalServiceException = null;

        foreach (var exchangeRateProvider in _exchangeRateProviders)
        {
            try
            {
                var fetchedRate = await exchangeRateProvider.GetExchangeRateAsync(baseCurrency, quoteCurrency, cancellationToken);
                if (fetchedRate is not null)
                {
                    return fetchedRate;
                }
            }
            catch (ExternalServiceException ex)
            {
                _logger.LogWarning(
                    ex,
                    ProviderFailureLogMessage,
                    exchangeRateProvider.GetType().Name,
                    baseCurrency,
                    quoteCurrency);

                lastExternalServiceException = ex;
            }
        }

        if (lastExternalServiceException is not null)
        {
            throw lastExternalServiceException;
        }

        return null;
    }

    private async Task<ExchangeRate> UpsertFetchedRateAsync(
        string baseCurrency,
        string quoteCurrency,
        ExchangeRate fetchedRate,
        CancellationToken cancellationToken)
    {
        var existingEntity = await _dbContext.ExchangeRates.FirstOrDefaultAsync(
            x => x.BaseCurrency == baseCurrency && x.QuoteCurrency == quoteCurrency,
            cancellationToken);

        var created = existingEntity is null;
        var entity = existingEntity ?? new ExchangeRate
        {
            BaseCurrency = baseCurrency,
            QuoteCurrency = quoteCurrency
        };

        if (created)
        {
            _dbContext.ExchangeRates.Add(entity);
        }

        entity.Bid = fetchedRate.Bid;
        entity.Ask = fetchedRate.Ask;
        entity.Provider = fetchedRate.Provider;
        entity.RetrievedAtUtc = fetchedRate.RetrievedAtUtc;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (created)
        {
            await _rateEventPublisher.PublishAsync(new ExchangeRateCreatedEvent(
                entity.BaseCurrency,
                entity.QuoteCurrency,
                entity.Bid,
                entity.Ask,
                entity.RetrievedAtUtc,
                entity.Provider), cancellationToken);
        }

        return entity;
    }

    private bool IsStale(ExchangeRate rate) =>
        DateTime.UtcNow - rate.RetrievedAtUtc >= _exchangeRateLookupOptions.StaleAfter;
}
