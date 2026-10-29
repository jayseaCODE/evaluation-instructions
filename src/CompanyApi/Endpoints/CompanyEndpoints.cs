using System.Globalization;
using CompanyApi.Models;
using CompanyApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CompanyApi.Endpoints;

/// <summary>
/// Maps the company HTTP endpoints.
/// </summary>
public static class CompanyEndpoints
{
    /// <summary>
    /// Maps <c>GET /v1/companies/{id}</c>.
    /// </summary>
    /// <param name="app">The route builder to add the endpoints to.</param>
    /// <returns>The same <paramref name="app"/>, for chaining.</returns>
    public static IEndpointRouteBuilder MapCompanies(this IEndpointRouteBuilder app)
    {
        var companies = app.MapGroup("/v1/companies").WithTags("Companies");

        companies.MapGet("/{id}", GetCompanyAsync)
            .WithName("GetCompany")
            .WithSummary("Gets a company by id.")
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError)
            .Produces<ErrorResponse>(StatusCodes.Status502BadGateway)
            .Produces<ErrorResponse>(StatusCodes.Status504GatewayTimeout);

        return app;
    }

    // The id is bound as a string so an invalid value gets this API's JSON 400 and not the framework's default error.
    private static async Task<Results<Ok<CompanyResponse>, NotFound<ErrorResponse>, BadRequest<ErrorResponse>>> GetCompanyAsync(
        string id,
        ICompanyService companyService,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var companyId))
            return TypedResults.BadRequest(new ErrorResponse(ErrorCodes.InvalidId, "The company id must be a non-negative integer."));

        var company = await companyService.GetCompanyAsync(companyId, cancellationToken);

        return company is null
            ? TypedResults.NotFound(new ErrorResponse(ErrorCodes.CompanyNotFound, "No company exists with the requested id."))
            : TypedResults.Ok(company);
    }
}
