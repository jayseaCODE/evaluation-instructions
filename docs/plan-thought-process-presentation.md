## Stage 1: Framing the problem

Brief: request in, call static XML service, return JSON matching [openapi-companies.yaml](../openapi-companies.yaml), with error handling and tests.

Constraints from the supplied files:

* Fixed output: integer `id`, three fields, no `Data` wrapper
* One known upstream shape: `Data` with `id`, `name`, `description`
* Output spec defines only a 404, so other failures need decisions

---

## Stage 2: Choosing the transformation approach

### Option 1: Generic XML-to-JSON converter

* Looks flexible, but the upstream only returns one shape
* Emits strings (`"1"` not `1`), keeps the root element, skips validation

### Option 2: Typed model mirroring the XML service

* Integer coercion, required-field checks, clear failure on bad payloads
* Easy to unit test; small model update if upstream changes

### Other options considered and rejected

* XSLT or XPath to JSON: overkill, hard to test
* Gateway-level transform: skips the application code the exercise asks for

### Outcome

Hardened XML parse, typed XML model, validation, then map to a separate JSON model. The output contract stays decoupled from the upstream format.

---

## Stage 3: Interpreting the observed upstream behaviour

| Observation                      | Consequence for the design                         |
|----------------------------------|----------------------------------------------------|
| Unknown ids return 404           | Map upstream 404 to the spec's own 404 `Error`     |
| Trailing `%` after the 404 body  | zsh artefact, not part of the body                 |
| 404 body is plain text           | Branch on status code only, never parse it         |
| Content type is likely text/plain | Do not require an XML content type on a 200       |
| `servers` URL contains `/blob/main` | Default to the curl form, keep it configurable  |

---

## Stage 4: Security reasoning

* SSRF: parse `id` to an integer and build the URL from that integer
* XXE and entity expansion: prohibit DTDs, disable the resolver, cap document size
* Disable redirects on the upstream client
* Never read non-200 bodies
* Keep error descriptions generic

Each control has a matching test (XXE payload, entity expansion, oversized document, path-traversal id).

---

## Stage 5: Designing the error contract

### Decisions

* Reuse the spec's `Error` schema (`error`, `error_description`) instead of RFC 7807
* Stable machine-readable codes
* Additive copy at `openapi-companies-extended.yaml` (repository root); original and [xml-api/openapi-xml.yaml](../xml-api/openapi-xml.yaml) untouched

### Why the path id becomes an integer

* Original spec types the path `id` as `number` but `Company.id` as `integer`
* `number` makes `/companies/1.5` valid, yet the service returns 400 for it
* Copy uses `integer`, `int32`, `minimum: 0` to match the real rule

### Why the file is named extended and not v2

Route, version, and success response are unchanged. Only error responses are added and `id` is tightened, so `extended` describes it accurately.

### Status code reasoning

| Status | Reason                                          |
|--------|-------------------------------------------------|
| 400    | Id is not a plain non-negative integer          |
| 404    | Upstream says the company does not exist        |
| 502    | Upstream failed, unreachable, or invalid XML    |
| 504    | Upstream exceeded the timeout                   |
| 500    | Unexpected internal failure                     |

Id `0` goes to the upstream so it stays the source of truth. Caller cancellation propagates and is not mapped to 504.

---

## Stage 6: Choosing the language and conventions

.NET, aligned to existing conventions:

* Repo C#: `net10.0`, C# 14, file-scoped namespaces, primary constructors, `src/` layout
* Tests: xunit with NSubstitute, `GivenContext_WhenAction_ExpectedResult`, Arrange/Act/Assert
* Web API: minimal APIs, `TypedResults`, global `IExceptionHandler`, `CancellationToken`, built-in OpenAPI, `.http` file

---

## Stage 7: Structuring the plan

| Phase | Theme                       | Why it sits here                          |
|-------|-----------------------------|-------------------------------------------|
| 0     | Scaffold                    | Blocks everything else                    |
| 1     | Contract and models         | Fixes output and error shapes first       |
| 2     | Upstream integration        | Core logic                                |
| 3     | HTTP surface                | Needs the service                         |
| 4     | Tests                       | Written alongside each unit               |
| 5     | Packaging and documentation | Can run in parallel with Phase 4          |
| 6     | Verification                | End-to-end contract and Docker check      |

### Design choices inside the phases

* Parser is a pure static function, so it is trivial to test
* Service returns `null` for not found, not an exception
* One `UpstreamException` with a kind carries the failure category
* `id` binds as a string so the service returns its own JSON 400
* No try/catch in endpoints; the global handler maps and logs
* `/healthz` is liveness only, so a GitHub outage does not restart instances
* Options use `ValidateOnStart` to fail fast on bad configuration

---

## Stage 8: Scope boundaries

Documented in `SOLUTION.md`, not coded:

* Retries and circuit breaking
* Short-TTL caching
* OpenTelemetry
* Rate limiting and authentication
* Publishing the API contract to internal and third-party consumers
### OpenTelemetry integration

What it would give:

* Server span per request, named by route template
* Client span per upstream call with status and duration
* Runtime and HTTP metrics, log correlation by trace id
* Optional counter for upstream failures by kind

Change is confined to `Program.cs`; the `/healthz` path is filtered out.

What must exist first:

* An OTLP backend, optionally behind a Collector
* `OTEL_EXPORTER_OTLP_ENDPOINT` and a matching protocol
* Credentials held in a secret store
* `OTEL_SERVICE_NAME` and resource attributes
* A deliberate sampling decision

All settings are environment variables, so the `Dockerfile` does not change.

Local verification: run an OTLP receiver, call ids 1, 0, and `abc`, confirm the traces, then stop the receiver and confirm requests still succeed.

### API contract strategy for consumers

Nothing here is built, because no integration is planned. It is recorded for discussion.

Principles:

* `openapi-companies-extended.yaml` is the single source of truth for the contract
* Changes stay additive within a major version; breaking changes need `/v2`
* The generated `/openapi/v1.json` is Development only, so production serves no spec

Which spec do internal developers use?

* The same YAML. Audiences differ in what is exposed and how it is distributed, not in the truth
* The generated document is not for consumers: it drifts (path `id` is a string, `description` is marked required) and has no examples
* Its useful role is verification: compare it with the YAML in CI
* Split public and internal documents only when internal-only endpoints exist

Internal consumers (future):

* Contract tests on both sides, not written until an integration exists
* Internal NuGet client generated from the YAML (Kiota or NSwag), so other .NET services call the API through a typed client instead of hand-written HTTP, models, and error handling
* Central registry, Spectral style guide, oasdiff breaking-change check on pull requests

Third parties (future):

* Stable public contract at a stable URL, published on Azure API Management or a developer portal
* Nothing exposed by accident: only intended operations, real `servers` URL
* Authentication in the spec (`securitySchemes`), plus rate limits
* Written versioning and deprecation policy with `Deprecation` and `Sunset` headers
* Sandbox or mock server so integrators build before they have credentials
* Generated client SDKs, versioned with the spec

Questions for discussion:

* Who owns the contract and reviews changes to it?
* What counts as a breaking change that forces `/v2`?
* When do public and internal documents need to be split?
* Should the gateway or the service enforce authentication and rate limits?
