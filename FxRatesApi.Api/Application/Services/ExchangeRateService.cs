using FxRatesApi.Api.Application.Dtos;
using FxRatesApi.Api.Application.Events;
using FxRatesApi.Api.Domain.Constants;
using FxRatesApi.Api.Domain.Models;
using FxRatesApi.Api.Domain.Utilities;
using FxRatesApi.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FxRatesApi.Api.Application.Services;

public class ExchangeRateService(
    AppDbContext dbContext,
    IExchangeRateResolver exchangeRateResolver,
    IRateEventPublisher rateEventPublisher,
    TimeProvider timeProvider) : IExchangeRateService
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly IExchangeRateResolver _exchangeRateResolver = exchangeRateResolver;
    private readonly IRateEventPublisher _rateEventPublisher = rateEventPublisher;
    private readonly TimeProvider _timeProvider = timeProvider;

    public Task<List<ExchangeRate>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _dbContext.ExchangeRates
            .AsNoTracking()
            .OrderBy(x => x.BaseCurrency)
            .ThenBy(x => x.QuoteCurrency)
            .ToListAsync(cancellationToken);

    public async Task<ExchangeRate> GetCurrentAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default)
    {
        var normalizedPair = NormalizePair(baseCurrency, quoteCurrency);
        var result = await _exchangeRateResolver.ResolveAsync(normalizedPair.BaseCurrency, normalizedPair.QuoteCurrency, cancellationToken);
        return result.Rate;
    }

    public async Task<ExchangeRateUpsertResult> UpsertAsync(UpsertExchangeRateRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedPair = NormalizePair(request.BaseCurrency, request.QuoteCurrency);

        return await UpsertInternalAsync(
            normalizedPair.BaseCurrency,
            normalizedPair.QuoteCurrency,
            request.Bid,
            request.Ask,
            request.Provider,
            cancellationToken);
    }

    public async Task<ExchangeRate?> UpdateAsync(
        string baseCurrency,
        string quoteCurrency,
        UpdateExchangeRateRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedPair = NormalizePair(baseCurrency, quoteCurrency);
        var entity = await FindTrackedByPairAsync(normalizedPair.BaseCurrency, normalizedPair.QuoteCurrency, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.Bid = request.Bid;
        entity.Ask = request.Ask;
        entity.Provider = NormalizeProvider(request.Provider, entity.Provider);
        entity.RetrievedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return entity;
    }

    public async Task<bool> DeleteAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default)
    {
        var normalizedPair = NormalizePair(baseCurrency, quoteCurrency);
        var entity = await FindTrackedByPairAsync(normalizedPair.BaseCurrency, normalizedPair.QuoteCurrency, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _dbContext.ExchangeRates.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<ExchangeRateUpsertResult> UpsertInternalAsync(
        string baseCurrency,
        string quoteCurrency,
        decimal bid,
        decimal ask,
        string? provider,
        CancellationToken cancellationToken)
    {
        var existingEntity = await FindTrackedByPairAsync(baseCurrency, quoteCurrency, cancellationToken);
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

        entity.Bid = bid;
        entity.Ask = ask;
        entity.Provider = NormalizeProvider(provider);
        entity.RetrievedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

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

        return new ExchangeRateUpsertResult(entity, created);
    }

    private async Task<ExchangeRate?> FindTrackedByPairAsync(
        string baseCurrency,
        string quoteCurrency,
        CancellationToken cancellationToken) =>
        await _dbContext.ExchangeRates.FirstOrDefaultAsync(
            x => x.BaseCurrency == baseCurrency && x.QuoteCurrency == quoteCurrency,
            cancellationToken);

    private static (string BaseCurrency, string QuoteCurrency) NormalizePair(string baseCurrency, string quoteCurrency) =>
        (
            CurrencyCodeUtility.Normalize(baseCurrency, nameof(baseCurrency)),
            CurrencyCodeUtility.Normalize(quoteCurrency, nameof(quoteCurrency))
        );

    private static string NormalizeProvider(string? value, string fallback = ExchangeRateProviders.Manual) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
