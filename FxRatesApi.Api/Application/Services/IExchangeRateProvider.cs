using FxRatesApi.Api.Domain.Models;

namespace FxRatesApi.Api.Application.Services;

public interface IExchangeRateProvider
{
    Task<ExchangeRate?> GetExchangeRateAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default);
}
