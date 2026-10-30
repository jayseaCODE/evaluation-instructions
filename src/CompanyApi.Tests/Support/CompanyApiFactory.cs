using CompanyApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyApi.Tests.Support;

/// <summary>
/// Hosts the Company API in memory. When a handler is supplied it replaces the upstream HTTP handler so no network is used.
/// </summary>
/// <param name="upstreamHandler">The stub that stands in for the upstream, or null to call the real upstream.</param>
public sealed class CompanyApiFactory(StubHttpMessageHandler? upstreamHandler = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (upstreamHandler is null)
            return;

        builder.ConfigureTestServices(services => services
            .AddHttpClient<ICompanyService, CompanyService>()
            .ConfigurePrimaryHttpMessageHandler(() => upstreamHandler));
    }
}
