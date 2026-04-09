namespace FxRatesApi.Api.Infrastructure.Configuration;

public class ExchangeRateLookupOptions
{
    public const string SectionName = "ExchangeRateLookup";

    public bool UseDatabaseCache { get; set; } = true;
    public bool PersistFetchedRates { get; set; } = true;
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(15);
}
