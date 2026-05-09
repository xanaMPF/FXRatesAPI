using FxRatesApi.Api.Application.Dtos;
using FxRatesApi.Api.Domain.Constants;
using FxRatesApi.Api.Domain.Models;

namespace FxRatesApi.Api.Application.Services;

public record ExchangeRateLookupResult(ExchangeRate Rate, string Source)
{
    public static ExchangeRateLookupResult FromDatabase(ExchangeRate rate) => new(rate, ExchangeRateSources.Database);

    public static ExchangeRateLookupResult FromProvider(ExchangeRate rate) => new(rate, ExchangeRateSources.ThirdPartyApi);
}

public record ExchangeRateUpsertResult(ExchangeRate Rate, bool Created);

public interface IExchangeRateService
{
    Task<List<ExchangeRate>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ExchangeRate> GetCurrentAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default);
    Task<ExchangeRateUpsertResult> UpsertAsync(UpsertExchangeRateRequest request, CancellationToken cancellationToken = default);
    Task<ExchangeRate?> UpdateAsync(string baseCurrency, string quoteCurrency, UpdateExchangeRateRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default);
}
