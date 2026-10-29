using System.Xml.Serialization;

namespace CompanyApi.Services;

/// <summary>
/// The XML shape returned by the upstream service. Members are nullable so that a missing element is detectable.
/// </summary>
[XmlRoot("Data")]
public sealed class XmlCompany
{
    /// <summary>The company identifier.</summary>
    [XmlElement("id")]
    public int? Id { get; set; }

    /// <summary>The company name.</summary>
    [XmlElement("name")]
    public string? Name { get; set; }

    /// <summary>The optional company description.</summary>
    [XmlElement("description")]
    public string? Description { get; set; }
}
