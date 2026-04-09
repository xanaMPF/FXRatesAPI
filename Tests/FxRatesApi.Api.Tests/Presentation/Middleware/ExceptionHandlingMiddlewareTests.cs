using System.Text;
using System.Text.Json;
using FxRatesApi.Api.Domain.Exceptions;
using FxRatesApi.Api.Presentation.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FxRatesApi.Api.Tests.Presentation.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int StatusCode, string Body)> InvokeWithException(Exception exception)
    {
        var context = new DefaultHttpContext();
        await using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        responseStream.Position = 0;
        using var reader = new StreamReader(responseStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return (context.Response.StatusCode, body);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsBadRequest_ForArgumentException()
    {
        var (statusCode, body) = await InvokeWithException(
            new ArgumentException("Currency code 'ABC' is not supported.", "baseCurrency"));

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.Contains("not supported", body);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsNotFound_ForKeyNotFoundException()
    {
        var (statusCode, body) = await InvokeWithException(
            new KeyNotFoundException("Rate not found."));

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
        Assert.Contains("Rate not found", body);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsConflict_ForResourceConflictException()
    {
        var (statusCode, body) = await InvokeWithException(
            new ResourceConflictException("Resource already exists."));

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        Assert.Contains("already exists", body);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsConflict_ForDbUpdateConcurrencyException()
    {
        var (statusCode, body) = await InvokeWithException(
            new DbUpdateConcurrencyException("Concurrency conflict."));

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        Assert.Contains("modified by another request", body);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsServiceUnavailable_ForExternalServiceException()
    {
        var (statusCode, body) = await InvokeWithException(
            new ExternalServiceException("Provider is down."));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusCode);
        Assert.Contains("Provider is down", body);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsInternalServerError_ForUnknownException()
    {
        var (statusCode, body) = await InvokeWithException(
            new InvalidOperationException("Something unexpected."));

        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.Contains("unexpected error", body);
    }

    [Fact]
    public async Task InvokeAsync_SetsContentTypeToJson()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new Exception("fail"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal("application/json", context.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsJsonWithMessageField()
    {
        var (_, body) = await InvokeWithException(new ArgumentException("Bad input."));

        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("message", out _));
    }

    [Fact]
    public async Task InvokeAsync_PassesThrough_WhenNoExceptionThrown()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var called = false;

        var middleware = new ExceptionHandlingMiddleware(
            _ => { called = true; return Task.CompletedTask; },
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.True(called);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }
}