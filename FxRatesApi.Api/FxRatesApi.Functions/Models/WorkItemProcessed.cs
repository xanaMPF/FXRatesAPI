using System;

namespace FxRatesApi.Api.FxRatesApi.Functions.Models;

public class WorkItemProcessed
{
    public Guid WorkItemId { get; set; }
    public DateTime ProcessedAt { get; set; }
}
