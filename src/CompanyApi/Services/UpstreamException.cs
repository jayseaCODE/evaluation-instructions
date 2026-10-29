namespace CompanyApi.Services;

/// <summary>
/// The category of an upstream failure.
/// </summary>
public enum UpstreamFailureKind
{
    /// <summary>The upstream returned an unexpected status or could not be reached.</summary>
    Unavailable,

    /// <summary>The upstream did not respond within the configured timeout.</summary>
    Timeout,

    /// <summary>The upstream returned a document that is not a valid company.</summary>
    InvalidResponse,
}

/// <summary>
/// Raised when the upstream company service fails, carrying the failure category to the error handler.
/// </summary>
/// <param name="kind">The category of the failure.</param>
/// <param name="message">A description for logs. It is never returned to callers.</param>
/// <param name="innerException">The underlying exception, if any.</param>
public sealed class UpstreamException(UpstreamFailureKind kind, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>Gets the category of the failure.</summary>
    public UpstreamFailureKind Kind { get; } = kind;
}
