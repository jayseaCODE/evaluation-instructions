namespace CompanyApi.Models;

/// <summary>
/// Machine-readable values for <see cref="ErrorResponse.Error"/>.
/// </summary>
public static class ErrorCodes
{
    /// <summary>The id in the request path is not a plain non-negative integer.</summary>
    public const string InvalidId = "invalid_id";

    /// <summary>The upstream reported that the company does not exist.</summary>
    public const string CompanyNotFound = "company_not_found";

    /// <summary>The upstream returned an unexpected status or could not be reached.</summary>
    public const string UpstreamUnavailable = "upstream_unavailable";

    /// <summary>The upstream returned a 200 response that is not a valid company document.</summary>
    public const string UpstreamInvalidResponse = "upstream_invalid_response";

    /// <summary>The upstream did not respond within the configured timeout.</summary>
    public const string UpstreamTimeout = "upstream_timeout";

    /// <summary>An unexpected error occurred inside this service.</summary>
    public const string InternalError = "internal_error";
}
