using System.Xml;
using System.Xml.Serialization;
using CompanyApi.Models;

namespace CompanyApi.Services;

/// <summary>
/// Converts an upstream XML document into a <see cref="CompanyResponse"/>.
/// </summary>
public static class XmlCompanyParser
{
    private static readonly XmlSerializer Serializer = new(typeof(XmlCompany));

    /// <summary>
    /// Parses a company XML document with DTD processing and external entity resolution disabled.
    /// </summary>
    /// <param name="stream">The XML document.</param>
    /// <param name="maxCharacters">The maximum number of characters accepted in the document.</param>
    /// <returns>The parsed company.</returns>
    /// <exception cref="UpstreamException">
    /// Thrown with <see cref="UpstreamFailureKind.InvalidResponse"/> when the document is malformed or lacks a required field.
    /// </exception>
    public static CompanyResponse Parse(Stream stream, int maxCharacters)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = maxCharacters,
        };

        XmlCompany? company;
        try
        {
            using var reader = XmlReader.Create(stream, settings);
            company = Serializer.Deserialize(reader) as XmlCompany;
        }
        catch (Exception ex) when (ex is InvalidOperationException or XmlException)
        {
            throw new UpstreamException(UpstreamFailureKind.InvalidResponse, "The upstream document is not valid XML.", ex);
        }

        if (company?.Id is not { } id)
            throw new UpstreamException(UpstreamFailureKind.InvalidResponse, "The upstream document has no valid id.");

        if (string.IsNullOrWhiteSpace(company.Name))
            throw new UpstreamException(UpstreamFailureKind.InvalidResponse, "The upstream document has no name.");

        return new CompanyResponse(id, company.Name, company.Description);
    }
}
