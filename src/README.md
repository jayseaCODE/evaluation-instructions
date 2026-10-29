## Overview

The Company API is an ASP.NET Core minimal API that exposes `GET /v1/companies/{id}`. It fetches the matching XML document from a static upstream and returns JSON that matches [openapi-companies.yaml](../openapi-companies.yaml). The design and build order are in [docs/implementation-plan.md](../docs/implementation-plan.md).

> [!NOTE]
> The project is being built phase by phase. The endpoint, upstream integration, and error handling (Phases 0 to 3) work against the real upstream. Automated tests and packaging (Phases 4 and 5) are not added yet.

## Prerequisites

* .NET SDK 10.0 or later. Check with `dotnet --list-sdks`.
* Git.
* Optional: Docker, once the `Dockerfile` is added in Phase 5.

## Getting started

Run all commands from the repository root.

1. Restore, build, and confirm the build has no warnings:

   ```bash
   dotnet build CompanyApi.sln
   ```

2. Run the tests:

   ```bash
   dotnet test CompanyApi.sln
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
