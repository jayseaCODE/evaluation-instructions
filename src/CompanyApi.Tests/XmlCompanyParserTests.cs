using System.Text;
using CompanyApi.Models;
using CompanyApi.Services;
using CompanyApi.Tests.Support;

namespace CompanyApi.Tests;

public class XmlCompanyParserTests
{
    private const int MaxCharacters = 10_000;

    private static MemoryStream ToStream(string xml) => new(Encoding.UTF8.GetBytes(xml));

    private static void AssertInvalidResponse(string xml, int maxCharacters = MaxCharacters)
    {
        var exception = Assert.Throws<UpstreamException>(() => XmlCompanyParser.Parse(ToStream(xml), maxCharacters));

        Assert.Equal(UpstreamFailureKind.InvalidResponse, exception.Kind);
    }

    [Theory]
    [InlineData("1.xml", 1, "MWNZ", "..is awesome")]
    [InlineData("2.xml", 2, "Other", "....is not")]
    public void GivenRealFixture_WhenParse_ReturnsCompany(string fileName, int id, string name, string description)
    {
        // Arrange
        using var stream = Fixtures.Open(fileName);

        // Act
        var actual = XmlCompanyParser.Parse(stream, MaxCharacters);

        // Assert
        Assert.Equal(new CompanyResponse(id, name, description), actual);
    }

    [Fact]
    public void GivenWhitespaceAroundId_WhenParse_ReturnsCompany()
    {
        // Arrange
        var xml = "<Data>\n\t<id> 7 </id>\n\t<name>Tabbed</name>\n</Data>";

        // Act
        var actual = XmlCompanyParser.Parse(ToStream(xml), MaxCharacters);

        // Assert
        Assert.Equal(7, actual.Id);
    }

    [Fact]
    public void GivenNoDescription_WhenParse_ReturnsNullDescription()
    {
        // Arrange
        var xml = "<Data><id>1</id><name>MWNZ</name></Data>";

        // Act
        var actual = XmlCompanyParser.Parse(ToStream(xml), MaxCharacters);

        // Assert
        Assert.Null(actual.Description);
    }

    [Fact]
    public void GivenExtraElements_WhenParse_IgnoresThem()
    {
        // Arrange
        var xml = "<Data><id>1</id><name>MWNZ</name><description>d</description><extra>1</extra></Data>";

        // Act
        var actual = XmlCompanyParser.Parse(ToStream(xml), MaxCharacters);

        // Assert
        Assert.Equal(new CompanyResponse(1, "MWNZ", "d"), actual);
    }

    [Theory]
    [InlineData("<Data><name>MWNZ</name></Data>")]
    [InlineData("<Data><id>abc</id><name>MWNZ</name></Data>")]
    [InlineData("<Data><id></id><name>MWNZ</name></Data>")]
    [InlineData("<Data><id>99999999999</id><name>MWNZ</name></Data>")]
    public void GivenMissingOrInvalidId_WhenParse_ThrowsInvalidResponse(string xml) => AssertInvalidResponse(xml);

    [Theory]
    [InlineData("<Data><id>1</id></Data>")]
    [InlineData("<Data><id>1</id><name></name></Data>")]
    [InlineData("<Data><id>1</id><name>   </name></Data>")]
    public void GivenMissingOrBlankName_WhenParse_ThrowsInvalidResponse(string xml) => AssertInvalidResponse(xml);

    [Theory]
    [InlineData("")]
    [InlineData("not xml")]
    [InlineData("<Data><id>1</id>")]
    public void GivenEmptyOrMalformedInput_WhenParse_ThrowsInvalidResponse(string xml) => AssertInvalidResponse(xml);

    [Fact]
    public void GivenWrongRootElement_WhenParse_ThrowsInvalidResponse() =>
        AssertInvalidResponse("<Other><id>1</id><name>MWNZ</name></Other>");

    [Fact]
    public void GivenExternalEntityPointingAtFile_WhenParse_ThrowsInvalidResponse() =>
        AssertInvalidResponse(Fixtures.XxePayload);

    [Fact]
    public void GivenExternalEntityPointingAtFile_WhenParse_DoesNotLeakFileContents()
    {
        // Arrange
        const string marker = "TOP-SECRET-MARKER";
        var secretFile = Path.GetTempFileName();
        File.WriteAllText(secretFile, marker);

        try
        {
            var xml = $"""<!DOCTYPE Data [ <!ENTITY x SYSTEM "{new Uri(secretFile).AbsoluteUri}"> ]><Data><id>1</id><name>&x;</name></Data>""";

            // Act
            var exception = Assert.Throws<UpstreamException>(() => XmlCompanyParser.Parse(ToStream(xml), MaxCharacters));

            // Assert
            Assert.DoesNotContain(marker, exception.ToString());
        }
        finally
        {
            File.Delete(secretFile);
        }
    }

    [Fact]
    public void GivenNestedEntityExpansion_WhenParse_ThrowsInvalidResponse()
    {
        // Arrange
        var xml = """
            <!DOCTYPE Data [
              <!ENTITY a "aaaaaaaaaa">
              <!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;">
              <!ENTITY c "&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;">
            ]>
            <Data><id>1</id><name>&c;</name></Data>
            """;

        // Act & Assert
        AssertInvalidResponse(xml);
    }

    [Fact]
    public void GivenDocumentOverCharacterLimit_WhenParse_ThrowsInvalidResponse() =>
        AssertInvalidResponse($"<Data><id>1</id><name>{new string('a', 5_000)}</name></Data>", maxCharacters: 1_000);
}
