using System.Threading.Channels;
using FxRatesApi.Api.Application.Events;
using Microsoft.Extensions.Hosting;

namespace FxRatesApi.Api.Infrastructure.Events;

public class InMemoryRateEventQueue(ILogger<InMemoryRateEventQueue> logger) : BackgroundService, IRateEventPublisher
{
    private readonly Channel<ExchangeRateCreatedEvent> _channel = Channel.CreateUnbounded<ExchangeRateCreatedEvent>();
    private readonly ILogger<InMemoryRateEventQueue> _logger = logger;

    public ValueTask PublishAsync(ExchangeRateCreatedEvent exchangeRateEvent, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(exchangeRateEvent, cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var exchangeRateEvent in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            _logger.LogInformation(
                "ExchangeRateCreated event: {BaseCurrency}/{QuoteCurrency} Bid={Bid} Ask={Ask} Source={Source} RetrievedAtUtc={RetrievedAtUtc}",
                exchangeRateEvent.BaseCurrency,
                exchangeRateEvent.QuoteCurrency,
                exchangeRateEvent.Bid,
                exchangeRateEvent.Ask,
                exchangeRateEvent.Source,
                exchangeRateEvent.RetrievedAtUtc);
        }
    }
}
