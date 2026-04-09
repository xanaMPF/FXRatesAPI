using System.Text;
using FxRatesApi.Api.Presentation.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace FxRatesApi.Api.Tests.Presentation.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsBadRequest_ForArgumentException()
    {
        var context = new DefaultHttpContext();
        await using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ArgumentException("Currency code 'ABC' is not supported.", "baseCurrency"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        responseStream.Position = 0;
        using var reader = new StreamReader(responseStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Contains("not supported", body);
    }
}