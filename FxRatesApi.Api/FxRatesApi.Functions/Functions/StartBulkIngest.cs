using System.IO;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;

namespace FxRatesApi.Api.Functions;

public class StartBulkIngest
{
    private readonly ILogger _logger;

    public StartBulkIngest(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<StartBulkIngest>();
    }

    [Function("StartBulkIngest")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "rates/bulk")] HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();

        // Minimal scaffold: accept the request and return a 202 with a JobId.
        var jobId = Guid.NewGuid();

        var response = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
        response.Headers.Add("Location", $"/api/jobs/{jobId}");
        await response.WriteStringAsync($"{{ \"jobId\": \"{jobId}\" }}");
        _logger.LogInformation("Accepted bulk ingest request, job {JobId}", jobId);
        return response;
    }
}
