namespace FxRatesApi.Api.Application.Events;

public record ExchangeRateCreatedEvent(
    string BaseCurrency,
    string QuoteCurrency,
    decimal Bid,
    decimal Ask,
    DateTime RetrievedAtUtc,
    string Source);

public interface IRateEventPublisher
{
    ValueTask PublishAsync(ExchangeRateCreatedEvent exchangeRateEvent, CancellationToken cancellationToken = default);
}
