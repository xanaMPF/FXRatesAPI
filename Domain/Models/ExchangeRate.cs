using FxRatesApi.Api.Domain.Constants;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FxRatesApi.Api.Domain.Models;

[Index(nameof(BaseCurrency), nameof(QuoteCurrency), IsUnique = true)]
public class ExchangeRate
{
    public int Id { get; set; }

    [Required]
    [MaxLength(3)]
    public string BaseCurrency { get; set; } = string.Empty;

    [Required]
    [MaxLength(3)]
    public string QuoteCurrency { get; set; } = string.Empty;

    public decimal Bid { get; set; }
    public decimal Ask { get; set; }

    [MaxLength(100)]
    public string Provider { get; set; } = ExchangeRateProviders.Manual;

    public DateTime RetrievedAtUtc { get; set; } = DateTime.UtcNow;
}
