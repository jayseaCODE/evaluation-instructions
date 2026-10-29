using System.Text.Json.Serialization;

namespace CompanyApi.Models;

/// <summary>
/// The error body returned for every failed request.
/// </summary>
/// <param name="Error">A stable machine-readable code from <see cref="ErrorCodes"/>.</param>
/// <param name="ErrorDescription">A safe, human-readable message that exposes no internal details.</param>
public sealed record ErrorResponse(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("error_description")] string ErrorDescription);
