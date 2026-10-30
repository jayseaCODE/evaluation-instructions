using System.Net;
using System.Net.Http.Json;
using CompanyApi.Models;
using CompanyApi.Tests.Support;

namespace CompanyApi.Tests;

public sealed class CompanyEndpointsTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly CompanyApiFactory _factory;
    private readonly StubHttpMessageHandler _handler = new();

    public CompanyEndpointsTests()
    {
        _factory = new CompanyApiFactory(_handler);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<ErrorResponse?> ReadErrorAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<ErrorResponse>();

    [Fact]
    public async Task GivenUpstreamHasCompany_WhenGetCompany_ReturnsExactJson()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, Fixtures.Read("1.xml"));

        // Act
        var response = await _client.GetAsync("/v1/companies/1");

        // Assert
        Assert.Equal("""{"id":1,"name":"MWNZ","description":"..is awesome"}""", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GivenUpstreamHasCompany_WhenGetCompany_ReturnsOkWithJsonContentType()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, Fixtures.Read("2.xml"));

        // Act
        var response = await _client.GetAsync("/v1/companies/2");

        // Assert
        Assert.Equal((HttpStatusCode.OK, "application/json"), (response.StatusCode, response.Content.Headers.ContentType?.MediaType));
    }

    [Fact]
    public async Task GivenUpstreamDocumentHasNoDescriptionElement_WhenGetCompany_OmitsDescriptionFromJson()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, "<Data><id>5</id><name>MWNZ</name></Data>");

        // Act
        var response = await _client.GetAsync("/v1/companies/5");

        // Assert
        Assert.Equal("""{"id":5,"name":"MWNZ"}""", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GivenUpstreamDocumentHasEmptyDescriptionElement_WhenGetCompany_ReturnsEmptyDescription()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, "<Data><id>5</id><name>MWNZ</name><description></description></Data>");

        // Act
        var response = await _client.GetAsync("/v1/companies/5");

        // Assert
        Assert.Equal("""{"id":5,"name":"MWNZ","description":""}""", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GivenIdWithLeadingZeros_WhenGetCompany_RequestsNormalisedUpstreamUrl()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, Fixtures.Read("1.xml"));

        // Act
        await _client.GetAsync("/v1/companies/01");

        // Assert
        Assert.EndsWith("/xml-api/1.xml", Assert.Single(_handler.RequestUris)?.AbsolutePath);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("+1")]
    [InlineData("1e3")]
    [InlineData("99999999999")]
    public async Task GivenInvalidId_WhenGetCompany_ReturnsBadRequestWithInvalidIdError(string id)
    {
        // Act
        var response = await _client.GetAsync($"/v1/companies/{id}");

        // Assert
        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.InvalidId), (response.StatusCode, (await ReadErrorAsync(response))?.Error));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    [InlineData("1.xml")]
    [InlineData("..%2F..%2Fsecret")]
    [InlineData("1%2F..%2F..%2Fsecret")]
    public async Task GivenInvalidId_WhenGetCompany_DoesNotCallUpstream(string id)
    {
        // Act
        await _client.GetAsync($"/v1/companies/{id}");

        // Assert
        Assert.Empty(_handler.RequestUris);
    }

    [Fact]
    public async Task GivenQueryStringOnRequest_WhenGetCompany_DoesNotForwardQueryUpstream()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, Fixtures.Read("1.xml"));

        // Act
        await _client.GetAsync("/v1/companies/1?x=y");

        // Assert
        Assert.Equal(string.Empty, Assert.Single(_handler.RequestUris)?.Query);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task GivenUpstreamReturnsNotFound_WhenGetCompany_ReturnsNotFoundWithCompanyNotFoundError(int id)
    {
        // Arrange
        _handler.Respond(HttpStatusCode.NotFound, "404: Not Found");

        // Act
        var response = await _client.GetAsync($"/v1/companies/{id}");

        // Assert
        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.CompanyNotFound), (response.StatusCode, (await ReadErrorAsync(response))?.Error));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "", ErrorCodes.UpstreamUnavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "", ErrorCodes.UpstreamUnavailable)]
    [InlineData(HttpStatusCode.OK, "not xml", ErrorCodes.UpstreamInvalidResponse)]
    [InlineData(HttpStatusCode.OK, "<Data><name>no id</name></Data>", ErrorCodes.UpstreamInvalidResponse)]
    [InlineData(HttpStatusCode.OK, Fixtures.XxePayload, ErrorCodes.UpstreamInvalidResponse)]
    public async Task GivenUpstreamFails_WhenGetCompany_ReturnsBadGatewayWithMatchingError(HttpStatusCode upstreamStatus, string body, string expectedError)
    {
        // Arrange
        _handler.Respond(upstreamStatus, body);

        // Act
        var response = await _client.GetAsync("/v1/companies/1");

        // Assert
        Assert.Equal((HttpStatusCode.BadGateway, expectedError), (response.StatusCode, (await ReadErrorAsync(response))?.Error));
    }

    [Fact]
    public async Task GivenUpstreamUnreachable_WhenGetCompany_ReturnsBadGatewayWithUnavailableError()
    {
        // Arrange
        _handler.Throw(new HttpRequestException("connection refused"));

        // Act
        var response = await _client.GetAsync("/v1/companies/1");

        // Assert
        Assert.Equal((HttpStatusCode.BadGateway, ErrorCodes.UpstreamUnavailable), (response.StatusCode, (await ReadErrorAsync(response))?.Error));
    }

    [Fact]
    public async Task GivenUpstreamTimesOut_WhenGetCompany_ReturnsGatewayTimeoutWithTimeoutError()
    {
        // Arrange
        _handler.Throw(new TaskCanceledException());

        // Act
        var response = await _client.GetAsync("/v1/companies/1");

        // Assert
        Assert.Equal((HttpStatusCode.GatewayTimeout, ErrorCodes.UpstreamTimeout), (response.StatusCode, (await ReadErrorAsync(response))?.Error));
    }

    [Fact]
    public async Task GivenUnexpectedException_WhenGetCompany_ReturnsInternalServerErrorWithoutExceptionMessage()
    {
        // Arrange
        _handler.Throw(new InvalidOperationException("secret internal detail"));

        // Act
        var response = await _client.GetAsync("/v1/companies/1");

        // Assert
        Assert.Equal(
            (HttpStatusCode.InternalServerError, ErrorCodes.InternalError, false),
            (response.StatusCode, (await ReadErrorAsync(response))?.Error, (await response.Content.ReadAsStringAsync()).Contains("secret")));
    }

    [Fact]
    public async Task GivenRunningService_WhenGetHealthz_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/healthz");

        // Assert
        Assert.Equal((HttpStatusCode.OK, "Healthy"), (response.StatusCode, await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task GivenDevelopmentEnvironment_WhenGetOpenApiDocument_DescribesCompanyRoute()
    {
        // Act
        var document = await _client.GetStringAsync("/openapi/v1.json");

        // Assert
        Assert.Contains("/v1/companies/{id}", document);
    }

    [Fact]
    public async Task GivenUnknownRoute_WhenGet_ReturnsEmptyNotFound()
    {
        // Act
        var response = await _client.GetAsync("/nope");

        // Assert
        Assert.Equal((HttpStatusCode.NotFound, string.Empty), (response.StatusCode, await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task GivenPostToCompanyRoute_WhenPost_ReturnsMethodNotAllowed()
    {
        // Act
        var response = await _client.PostAsync("/v1/companies/1", content: null);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
