using System.ComponentModel.DataAnnotations;

namespace CompanyApi.Services;

/// <summary>
/// Settings for the upstream XML company service, bound from the <c>Upstream</c> configuration section.
/// </summary>
public sealed class UpstreamOptions
{
    /// <summary>The configuration section that holds these settings.</summary>
    public const string SectionName = "Upstream";

    /// <summary>
    /// The base URL that company documents are resolved against, for example <c>https://host/xml-api/</c>.
    /// </summary>
    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The maximum time, in seconds, to wait for the upstream to deliver a complete response.</summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>The maximum number of characters accepted in an upstream XML document.</summary>
    [Range(1_000, 1_000_000)]
    public int MaxDocumentCharacters { get; set; } = 10_000;
}
