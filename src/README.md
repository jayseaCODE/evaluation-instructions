## Overview

The Company API is an ASP.NET Core minimal API that exposes `GET /v1/companies/{id}`. It fetches the matching XML document from a static upstream and returns JSON that matches [openapi-companies.yaml](../openapi-companies.yaml). The design and build order are in [docs/implementation-plan.md](../docs/implementation-plan.md).

> [!NOTE]
> The project is being built phase by phase. The endpoint, upstream integration, error handling, automated tests, container image, CI pipeline, and production deployment guide are in place.

## Prerequisites

* .NET SDK 10.0 or later. Check with `dotnet --list-sdks`.
* Git.
* Optional: Docker, to build and run the container image.

## Getting started

Run all commands from the repository root.

1. Restore, build, and confirm the build has no warnings:

   ```bash
   dotnet build CompanyApi.sln
   ```

2. Run the tests. The default run uses a stubbed upstream and needs no network:

   ```bash
   dotnet test CompanyApi.sln --filter "Category!=Live"
   ```

   The live tests call the real upstream on GitHub and run only on request:

   ```bash
   dotnet test CompanyApi.sln --filter "Category=Live"
   ```

3. Run the API:

   ```bash
   dotnet run --project src/CompanyApi --launch-profile http
   ```

   The service listens on `http://localhost:5143`.

4. Call it from a second terminal:

   ```bash
   curl http://localhost:5143/v1/companies/1
   curl http://localhost:5143/v1/companies/0
   curl http://localhost:5143/v1/companies/abc
   ```

   Ids 1 and 2 return a company, ids with no upstream document (such as 0 and 3) return a 404, and anything that is not a non-negative integer returns a 400. Every error has the body `{ "error": "<code>", "error_description": "<message>" }`. [src/CompanyApi/CompanyApi.http](CompanyApi/CompanyApi.http) has ready-made requests for VS Code (REST Client) and Visual Studio.

   In Development, `/openapi/v1.json` serves the generated OpenAPI document and `/healthz` reports liveness.

## Running in Docker

The [Dockerfile](../Dockerfile) builds a small image that runs the API as a non-root user. To deploy it to production, follow the [production deployment guide](../docs/deployment-guide.md). Run all commands from the repository root. Docker must be running.

1. Build the image:

   ```bash
   docker build --tag company-api:local .
   ```

   The build restores and publishes the API in a build stage, then copies only the published output into the ASP.NET runtime image.

2. Start the container:

   ```bash
   docker run --detach --rm --name company-api --publish 8080:8080 company-api:local
   ```

   The service listens on port 8080 inside the container, published to port 8080 on your machine. The container runs in the Production environment, so `/openapi/v1.json` is not served.

3. Wait until the service is ready. Startup takes a few seconds, and requests sent before then fail with a connection error:

   ```bash
   curl --silent --fail --retry 30 --retry-all-errors --retry-delay 1 http://localhost:8080/healthz
   ```

   The command prints `Healthy` when the service is up.

4. Run the same four cases as the live tests. The `-w` option prints the HTTP status after each body:

   ```bash
   for id in 1 2 0 3; do
     curl --silent --write-out ' [%{http_code}]\n' "http://localhost:8080/v1/companies/$id"
   done
   ```

   Expect these results:

   * Id 1 returns 200 with `{"id":1,"name":"MWNZ","description":"..is awesome"}`.
   * Id 2 returns 200 with `{"id":2,"name":"Other","description":"....is not"}`.
   * Ids 0 and 3 return 404 with `{"error":"company_not_found","error_description":"No company exists with the requested id."}`.

5. Optionally check the other error cases:

   ```bash
   curl --silent --write-out ' [%{http_code}]\n' http://localhost:8080/v1/companies/abc
   ```

   An id that is not a non-negative integer returns 400 with the `invalid_id` error. To see the upstream failures, start extra containers with a different `Upstream__BaseUrl`. Settings are read from environment variables, and `Upstream__BaseUrl` sets `Upstream:BaseUrl`:

   ```bash
   # Unreachable upstream: expect 502 with upstream_unavailable
   docker run --detach --rm --name company-api-502 --publish 8081:8080 \
     --env Upstream__BaseUrl=http://localhost:1/xml-api company-api:local

   # Non-routable upstream with a 2 second timeout: expect 504 with upstream_timeout after about 2 seconds
   docker run --detach --rm --name company-api-504 --publish 8082:8080 \
     --env Upstream__BaseUrl=http://10.255.255.1/xml-api --env Upstream__TimeoutSeconds=2 \
     company-api:local

   curl --silent --write-out ' [%{http_code}]\n' http://localhost:8081/v1/companies/1
   curl --silent --write-out ' [%{http_code}]\n' http://localhost:8082/v1/companies/1
   ```

   Wait a few seconds after starting them, as in step 3, before calling them.

6. View the logs and stop the containers. The `--rm` option removes each container when it stops:

   ```bash
   docker logs company-api
   docker stop company-api company-api-502 company-api-504
   ```

### Docker troubleshooting

* `Cannot connect to the Docker daemon` means Docker is not running. Start Docker Desktop and try again.
* `Connection reset` or an empty reply right after `docker run` means the service is still starting. Repeat the wait command in step 3.
* `port is already allocated` means another process uses the port. Change the left side of `--publish`, for example `--publish 9090:8080`, and use that port in the `curl` commands.
* `The container name is already in use` means an earlier container is still running. Stop it with `docker stop company-api`.

## Debugging in VS Code

The repository includes a debug configuration in [.vscode/launch.json](../.vscode/launch.json) and a build task in [.vscode/tasks.json](../.vscode/tasks.json).

### Requirements

* The [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp) (`ms-dotnettools.csharp`), which provides the .NET debugger. C# Dev Kit (`ms-dotnettools.csdevkit`) is optional and adds the Test Explorer.
* Open the repository root as the workspace folder, so `${workspaceFolder}` resolves correctly.

### Debug the API

1. Open the Run and Debug view (`Cmd+Shift+D` on macOS, `Ctrl+Shift+D` elsewhere).
2. Select **Debug CompanyApi** and press `F5`.
3. VS Code builds the project first, then starts the service on `http://localhost:5143` with `ASPNETCORE_ENVIRONMENT=Development`.
4. Set a breakpoint by clicking the gutter next to a line, then send a request, for example `curl http://localhost:5143/`.

Stop debugging with `Shift+F5`.

The configuration launches the compiled `CompanyApi.dll` directly, so it does not read `launchSettings.json`. The environment and URL are set in `launch.json` instead and match the `http` profile.

### Debug the tests

With C# Dev Kit installed, open the Testing view, then right-click a test and choose **Debug Test**. Without it, run `dotnet test` from the terminal and attach using the steps below.

### Attach to a running process

If the service was started with `dotnet run`, choose **Run and Debug**, then **.NET: Attach to process** (create it with **Add Configuration** if it is missing), and pick the `CompanyApi` process.

### Troubleshooting

* If the debugger says the program path does not exist, run `dotnet build CompanyApi.sln` once. The path in `launch.json` assumes the `Debug` configuration and the `net10.0` target.
* If port 5143 is already in use, stop any `dotnet run` session that is still running.
* Breakpoints appear hollow when the source does not match the loaded build. Rebuild, and make sure you are debugging the Debug configuration rather than a Release build.

## Repository layout

```text
CompanyApi.sln                  Solution file
Directory.Build.props           Shared build settings for every project
Dockerfile                      Container image for the API
.github/workflows/ci.yml        Build and test pipeline
docs/                           Implementation plan and design notes
openapi-companies.yaml          JSON contract the service must satisfy (do not edit)
openapi-companies-extended.yaml Same contract plus the documented error responses
xml-api/                        Upstream XML spec and sample payloads
src/
  CompanyApi/                   The web API
  CompanyApi.Tests/             xunit test project
```

## Conventions

* The target is `net10.0` with nullable reference types and implicit usings, configured once in [Directory.Build.props](../Directory.Build.props).
* Warnings are errors. A change that introduces a compiler warning fails the build.
* XML documentation is generated, so public members in `CompanyApi` need `///` comments. The test project suppresses this rule (`CS1591`).
* Tests use xunit and NSubstitute. Name them `GivenContext_WhenAction_ExpectedResult` and structure them as Arrange, Act, Assert.
* The original `openapi-companies.yaml` is never edited. Contract changes go in [openapi-companies-extended.yaml](../openapi-companies-extended.yaml).

## Architecture

This section will describe the request flow, the components, and the error mapping once they are built. Until then, the planned design is in the [implementation plan](../docs/implementation-plan.md#architecture).
