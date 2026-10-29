using CompanyApi.Endpoints;
using CompanyApi.Middleware;
using CompanyApi.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<UpstreamOptions>()
    .Bind(builder.Configuration.GetSection(UpstreamOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddHttpClient<ICompanyService, CompanyService>((serviceProvider, client) =>
    {
        var baseUrl = serviceProvider.GetRequiredService<IOptions<UpstreamOptions>>().Value.BaseUrl;

        // Relative request URIs only resolve against the final path segment when the base ends with a slash.
        client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
// UseExceptionHandler() refuses to start without this, even though ApiExceptionHandler handles every exception.
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapHealthChecks("/healthz");
app.MapCompanies();

app.Run();

/// <summary>
/// The application entry point, exposed so integration tests can host it.
/// </summary>
public partial class Program;
