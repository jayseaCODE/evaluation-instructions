using CompanyApi.Models;

namespace CompanyApi.Services;

/// <summary>
/// Retrieves companies from the upstream XML service.
/// </summary>
public interface ICompanyService
{
    /// <summary>
    /// Gets a company by id.
    /// </summary>
    /// <param name="id">The non-negative company identifier.</param>
    /// <param name="cancellationToken">Token that cancels the request when the caller disconnects.</param>
    /// <returns>The company, or <see langword="null"/> when the upstream reports it does not exist.</returns>
    /// <exception cref="UpstreamException">Thrown when the upstream fails, times out, or returns an invalid document.</exception>
    Task<CompanyResponse?> GetCompanyAsync(int id, CancellationToken cancellationToken);
}
