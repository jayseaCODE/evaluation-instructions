using System.Net;
using CompanyApi.Models;
using CompanyApi.Services;
using CompanyApi.Tests.Support;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CompanyApi.Tests;

public class CompanyServiceTests
{
    private static readonly Uri BaseAddress = new("https://upstream.test/xml-api/");

    private readonly StubHttpMessageHandler _handler = new();
    private readonly IOptions<UpstreamOptions> _options = Substitute.For<IOptions<UpstreamOptions>>();
    private readonly CompanyService _sut;

    public CompanyServiceTests()
    {
        _options.Value.Returns(new UpstreamOptions { BaseUrl = BaseAddress.AbsoluteUri });
        _sut = new CompanyService(new HttpClient(_handler) { BaseAddress = BaseAddress }, _options);
    }

    private async Task<UpstreamException> GetFailureAsync() =>
        await Assert.ThrowsAsync<UpstreamException>(() => _sut.GetCompanyAsync(1, CancellationToken.None));

    [Fact]
    public async Task GivenUpstreamReturnsCompanyXml_WhenGetCompanyAsync_ReturnsCompany()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, Fixtures.Read("1.xml"));

        // Act
        var actual = await _sut.GetCompanyAsync(1, CancellationToken.None);

        // Assert
        Assert.Equal(new CompanyResponse(1, "MWNZ", "..is awesome"), actual);
    }

    [Fact]
    public async Task GivenUpstreamReturnsNotFound_WhenGetCompanyAsync_ReturnsNull()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.NotFound, "404: Not Found");

        // Act
        var actual = await _sut.GetCompanyAsync(3, CancellationToken.None);

        // Assert
        Assert.Null(actual);
    }

    [Fact]
    public async Task GivenCompanyId_WhenGetCompanyAsync_RequestsExactUpstreamUrl()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, Fixtures.Read("1.xml"));

        // Act
        await _sut.GetCompanyAsync(1, CancellationToken.None);

        // Assert
        Assert.Equal(new Uri("https://upstream.test/xml-api/1.xml"), Assert.Single(_handler.RequestUris));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Found)]
    public async Task GivenUnexpectedStatus_WhenGetCompanyAsync_ThrowsUnavailable(HttpStatusCode statusCode)
    {
        // Arrange
        _handler.Respond(statusCode);

        // Act
        var actual = await GetFailureAsync();

        // Assert
        Assert.Equal(UpstreamFailureKind.Unavailable, actual.Kind);
    }

    [Fact]
    public async Task GivenUpstreamUnreachable_WhenGetCompanyAsync_ThrowsUnavailable()
    {
        // Arrange
        _handler.Throw(new HttpRequestException("connection refused"));

        // Act
        var actual = await GetFailureAsync();

        // Assert
        Assert.Equal(UpstreamFailureKind.Unavailable, actual.Kind);
    }

    [Fact]
    public async Task GivenInvalidXmlWithOkStatus_WhenGetCompanyAsync_ThrowsInvalidResponse()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, "not xml");

        // Act
        var actual = await GetFailureAsync();

        // Assert
        Assert.Equal(UpstreamFailureKind.InvalidResponse, actual.Kind);
    }

    [Fact]
    public async Task GivenBodyOverSizeLimit_WhenGetCompanyAsync_ThrowsInvalidResponse()
    {
        // Arrange
        _options.Value.Returns(new UpstreamOptions { MaxDocumentCharacters = 10 });
        _handler.Respond(HttpStatusCode.OK, new string('a', 100));

        // Act
        var actual = await GetFailureAsync();

        // Assert
        Assert.Equal(UpstreamFailureKind.InvalidResponse, actual.Kind);
    }

    [Fact]
    public async Task GivenHandlerReportsTimeout_WhenGetCompanyAsync_ThrowsTimeout()
    {
        // Arrange
        _handler.Throw(new TaskCanceledException());

        // Act
        var actual = await GetFailureAsync();

        // Assert
        Assert.Equal(UpstreamFailureKind.Timeout, actual.Kind);
    }

    [Fact]
    public async Task GivenUpstreamNeverResponds_WhenGetCompanyAsync_ThrowsTimeout()
    {
        // Arrange
        _options.Value.Returns(new UpstreamOptions { TimeoutSeconds = 1 });
        _handler.NeverRespond();

        // Act
        var actual = await GetFailureAsync();

        // Assert
        Assert.Equal(UpstreamFailureKind.Timeout, actual.Kind);
    }

    [Fact]
    public async Task GivenUpstreamStallsAfterHeaders_WhenGetCompanyAsync_ThrowsTimeout()
    {
        // Arrange
        _options.Value.Returns(new UpstreamOptions { TimeoutSeconds = 1 });
        _handler.StallBody();

        // Act
        var actual = await GetFailureAsync();

        // Assert
        Assert.Equal(UpstreamFailureKind.Timeout, actual.Kind);
    }

    [Fact]
    public async Task GivenCallerCancels_WhenGetCompanyAsync_PropagatesCancellation()
    {
        // Arrange
        _handler.NeverRespond();
        using var cancellation = new CancellationTokenSource();
        var pending = _sut.GetCompanyAsync(1, cancellation.Token);

        // Act
        await cancellation.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task GivenNegativeId_WhenGetCompanyAsync_ThrowsArgumentOutOfRange() =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _sut.GetCompanyAsync(-1, CancellationToken.None));
}
