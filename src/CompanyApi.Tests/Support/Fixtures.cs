namespace CompanyApi.Tests.Support;

/// <summary>
/// Shared test documents.
/// </summary>
public static class Fixtures
{
    public const string XxePayload =
        """
        <?xml version="1.0"?>
        <!DOCTYPE Data [ <!ENTITY x SYSTEM "file:///etc/passwd"> ]>
        <Data><id>1</id><name>&x;</name></Data>
        """;

    public static string Read(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));

    public static Stream Open(string fileName) =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
}
