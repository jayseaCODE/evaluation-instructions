using CompanyApi.Models;
using CompanyApi.Services;
using Microsoft.AspNetCore.Diagnostics;

namespace CompanyApi.Middleware;

/// <summary>
/// Converts unhandled exceptions into <see cref="ErrorResponse"/> JSON bodies.
/// </summary>
/// <param name="logger">The logger used to record the exception before the response is written.</param>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Returning true suppresses the default diagnostics, so the exception is logged here first.
        if (exception is UpstreamException)
            logger.LogWarning(exception, "The upstream company service failed.");
        else
            logger.LogError(exception, "An unhandled exception occurred.");

        var (status, error) = exception switch
        {
            UpstreamException { Kind: UpstreamFailureKind.Timeout } => (
                StatusCodes.Status504GatewayTimeout,
                new ErrorResponse(ErrorCodes.UpstreamTimeout, "The company service did not respond in time.")),
            UpstreamException { Kind: UpstreamFailureKind.InvalidResponse } => (
                StatusCodes.Status502BadGateway,
                new ErrorResponse(ErrorCodes.UpstreamInvalidResponse, "The company service returned an invalid response.")),
            UpstreamException => (
                StatusCodes.Status502BadGateway,
                new ErrorResponse(ErrorCodes.UpstreamUnavailable, "The company service is currently unavailable.")),
            _ => (
                StatusCodes.Status500InternalServerError,
                new ErrorResponse(ErrorCodes.InternalError, "An unexpected error occurred.")),
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(error, cancellationToken);
        return true;
    }
}
