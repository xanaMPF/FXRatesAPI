using System.ComponentModel.DataAnnotations;
using FxRatesApi.Api.Domain.Constants;

namespace FxRatesApi.Api.Application.Dtos;

public record UpsertExchangeRateRequest
{
    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string BaseCurrency { get; init; } = string.Empty;

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string QuoteCurrency { get; init; } = string.Empty;

    [Range(typeof(decimal), "0", "999999999")]
    public decimal Bid { get; init; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal Ask { get; init; }

    [StringLength(100)]
    public string? Provider { get; init; } = ExchangeRateProviders.Manual;
}