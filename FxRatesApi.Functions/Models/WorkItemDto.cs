using System;

namespace FxRatesApi.Api.FxRatesApi.Functions.Models;

public class WorkItemDto
{
    public Guid Id { get; set; }
    public string BaseCurrency { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = string.Empty;
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
}
