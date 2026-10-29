using System.Text.Json.Serialization;

namespace CompanyApi.Models;

/// <summary>
/// A company.
/// </summary>
/// <param name="Id">The company identifier.</param>
/// <param name="Name">The company name.</param>
/// <param name="Description">The company description, omitted from the JSON when not supplied.</param>
public sealed record CompanyResponse(
    int Id,
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description);
