using System.Globalization;
using System.Net;
using CompanyApi.Models;
using Microsoft.Extensions.Options;

namespace CompanyApi.Services;

/// <summary>
/// Fetches company documents from the upstream XML service and maps them to <see cref="CompanyResponse"/>.
/// </summary>
/// <param name="httpClient">A client whose <see cref="HttpClient.BaseAddress"/> ends with a slash.</param>
/// <param name="options">The upstream settings.</param>
public sealed class CompanyService(HttpClient httpClient, IOptions<UpstreamOptions> options) : ICompanyService
{
    private const int BufferSize = 8192;

    /// <inheritdoc/>
    public async Task<CompanyResponse?> GetCompanyAsync(int id, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(id);

        var settings = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));

        var requestUri = new Uri(string.Create(CultureInfo.InvariantCulture, $"{id}.xml"), UriKind.Relative);

        try
        {
            using var response = await httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (response.StatusCode != HttpStatusCode.OK)
                throw new UpstreamException(UpstreamFailureKind.Unavailable, $"The upstream returned status {(int)response.StatusCode}.");

            await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffered = await ReadBoundedAsync(body, settings.MaxDocumentCharacters, timeout.Token);
            return XmlCompanyParser.Parse(buffered, settings.MaxDocumentCharacters);
        }
        catch (HttpRequestException ex)
        {
            throw new UpstreamException(UpstreamFailureKind.Unavailable, "The upstream could not be reached.", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpstreamException(UpstreamFailureKind.Timeout, "The upstream did not respond in time.", ex);
        }
    }

    // UTF-8 uses at most 4 bytes per character, so this bounds buffering; the XML reader enforces the exact character cap.
    private static async Task<MemoryStream> ReadBoundedAsync(Stream source, int maxCharacters, CancellationToken cancellationToken)
    {
        var maxBytes = (long)maxCharacters * 4;
        var buffered = new MemoryStream();
        var buffer = new byte[BufferSize];

        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (buffered.Length + read > maxBytes)
            {
                await buffered.DisposeAsync();
                throw new UpstreamException(UpstreamFailureKind.InvalidResponse, "The upstream document exceeds the size limit.");
            }

            buffered.Write(buffer, 0, read);
        }

        buffered.Position = 0;
        return buffered;
    }
}
