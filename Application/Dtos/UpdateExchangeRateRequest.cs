using System.ComponentModel.DataAnnotations;

namespace FxRatesApi.Api.Application.Dtos;

public record UpdateExchangeRateRequest
{
    [Range(typeof(decimal), "0", "999999999")]
    public decimal Bid { get; init; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal Ask { get; init; }

    [StringLength(100)]
    public string? Provider { get; init; }
}
