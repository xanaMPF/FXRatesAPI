namespace FxRatesApi.Api.Application.Services;

public interface IExchangeRateResolver
{
    Task<ExchangeRateLookupResult> ResolveAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default);
}
