namespace FxRatesApi.Api.Domain.Exceptions;

public class ResourceConflictException : Exception
{
    public ResourceConflictException(string message)
        : base(message)
    {
    }

    public ResourceConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
