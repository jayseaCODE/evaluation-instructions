using System.Net;
using System.Net.Http.Json;
using CompanyApi.Models;
using CompanyApi.Tests.Support;

namespace CompanyApi.Tests;

/// <summary>
/// Calls the real upstream. Excluded from the default run with <c>--filter "Category!=Live"</c>.
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveUpstreamTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly CompanyApiFactory _factory;

    public LiveUpstreamTests()
    {
        _factory = new CompanyApiFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Theory]
    [InlineData(1, "MWNZ", "..is awesome")]
    [InlineData(2, "Other", "....is not")]
    public async Task GivenRealUpstream_WhenGetCompany_ReturnsDocumentedCompany(int id, string name, string description)
    {
        // Act
        var actual = await _client.GetFromJsonAsync<CompanyResponse>($"/v1/companies/{id}");

        // Assert
        Assert.Equal(new CompanyResponse(id, name, description), actual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task GivenRealUpstreamMissingDocument_WhenGetCompany_ReturnsNotFound(int id)
    {
        // Act
        var response = await _client.GetAsync($"/v1/companies/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
