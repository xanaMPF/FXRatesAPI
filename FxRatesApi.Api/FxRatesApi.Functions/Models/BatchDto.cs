using System.Collections.Generic;

namespace FxRatesApi.Api.FxRatesApi.Functions.Models;

public class BatchDto
{
    public List<WorkItemDto> Items { get; set; } = new();
}
