namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when authentication fails or is missing. Surfaces as HTTP 401.
/// </summary>
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}
