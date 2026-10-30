---
title: Production deployment guide
description: Step-by-step guide to building, configuring, deploying, verifying, and operating the Company API container in a production setting
author: Joshua Chen
ms.date: 2026-10-30
ms.topic: how-to
keywords:
  - docker
  - deployment
  - production
  - kubernetes
  - azure container apps
---

## Overview

The Company API is a stateless container. It needs no database and no secrets. It receives HTTP on port 8080, makes outbound HTTPS calls to the upstream XML service, and writes logs to standard output. That makes it safe to run several copies behind a load balancer and to replace a copy at any time.

This guide is platform neutral, followed by a worked Azure Container Apps example. For local build and run steps, see [src/README.md](../src/README.md#running-in-docker).

## Before you start

* A container registry you can push to (Azure Container Registry, GitHub Container Registry, or similar).
* A container host: Azure Container Apps, Kubernetes, App Service for Containers, or any engine that runs OCI images.
* A load balancer, ingress, or API gateway that terminates TLS. The container serves plain HTTP only.
* Outbound HTTPS access from the host to the upstream (`raw.githubusercontent.com` by default).
* A green [CI run](../.github/workflows/ci.yml) for the commit you are releasing.

## Step 1: Confirm the release commit is green

1. Open the pull request or the `main` commit and check that both CI jobs passed: `Build and test` and `Build and smoke-test container`.
2. Do not release a commit that has no passing run.

## Step 2: Build and tag the image

Tag every image with an immutable version, such as a release number or the Git commit SHA. Never deploy `latest`, because it makes it impossible to tell what is running or to roll back.

```bash
VERSION=1.0.0
docker build --tag <registry>/company-api:$VERSION .
```

## Step 3: Scan and push

1. Scan the image for known vulnerabilities and fix or accept the findings before release. For example, with Docker Scout or Trivy:

   ```bash
   docker scout cves <registry>/company-api:$VERSION
   ```

2. Push the image:

   ```bash
   docker push <registry>/company-api:$VERSION
   ```

## Step 4: Configure the service

All settings are environment variables. Nothing is baked into the image, so the same image runs in every environment.

* `Upstream__BaseUrl` is the base URL of the XML service. The default is `https://raw.githubusercontent.com/MiddlewareNewZealand/evaluation-instructions/main/xml-api/`.
* `Upstream__TimeoutSeconds` is the time allowed for a complete upstream response. The default is 10 and the allowed range is 1 to 120.
* `Upstream__MaxDocumentCharacters` caps the size of an upstream document. The default is 10000 and the allowed range is 1000 to 1000000.
* `ASPNETCORE_HTTP_PORTS` is the listening port. The image sets it to 8080.
* `ASPNETCORE_ENVIRONMENT` defaults to `Production` in the container. Leave it unset. Setting it to `Development` would serve the generated OpenAPI document.
* `Logging__LogLevel__Default` changes the log level, for example `Warning` to reduce volume.

The service validates the `Upstream__*` settings at startup. An invalid value stops the container immediately with a clear message in the logs, which is better than failing on the first request. Treat a container that exits at startup as a configuration error and read its logs.

No setting here is secret. If you later add credentials (for example an API key for the upstream), supply them from the platform's secret store and never from the image or the repository.

## Step 5: Run with a hardened configuration

The service needs no write access to its filesystem, no Linux capabilities, and very little memory. A restrictive run command is therefore safe, and it limits the damage if the service is ever compromised:

```bash
docker run --detach --name company-api --publish 8080:8080 \
  --read-only --tmpfs /tmp \
  --cap-drop ALL --security-opt no-new-privileges:true \
  --memory 256m --cpus 0.5 \
  --restart unless-stopped \
  <registry>/company-api:$VERSION
```

During testing this configuration used about 30 MB of memory. Set requests and limits with headroom above what you observe under load.

The image already runs as the non-root `app` user and sets `DOTNET_EnableDiagnostics=0`, which turns off the runtime's diagnostic channel. If you need to attach diagnostic tools to a running instance, override that variable for the investigation only.

## Step 6: Configure health probes

Use `/healthz` on port 8080 for all probes.

* The endpoint reports liveness only. It does not call the upstream, so an upstream outage does not mark healthy instances as failed or cause restart loops.
* Allow time for startup. The service takes a few seconds to begin listening, so use a startup probe or an initial delay of at least 10 seconds and a failure threshold that tolerates about 30 seconds.
* Because the probe ignores the upstream, watch upstream health through error rates instead (see step 9).

A Kubernetes example that applies the probes, resource limits, and the same hardening:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: company-api
spec:
  replicas: 2
  selector:
    matchLabels:
      app: company-api
  template:
    metadata:
      labels:
        app: company-api
    spec:
      containers:
        - name: company-api
          image: <registry>/company-api:1.0.0
          ports:
            - containerPort: 8080
          env:
            - name: Upstream__TimeoutSeconds
              value: "10"
          startupProbe:
            httpGet: { path: /healthz, port: 8080 }
            periodSeconds: 2
            failureThreshold: 15
          livenessProbe:
            httpGet: { path: /healthz, port: 8080 }
            periodSeconds: 10
          readinessProbe:
            httpGet: { path: /healthz, port: 8080 }
            periodSeconds: 5
          resources:
            requests: { cpu: 100m, memory: 64Mi }
            limits: { cpu: 500m, memory: 256Mi }
          securityContext:
            runAsNonRoot: true
            readOnlyRootFilesystem: true
            allowPrivilegeEscalation: false
            capabilities:
              drop: [ALL]
          volumeMounts:
            - name: tmp
              mountPath: /tmp
      volumes:
        - name: tmp
          emptyDir: {}
```

## Step 7: Expose the service securely

1. Put the service behind an ingress, load balancer, or API gateway that terminates TLS. Do not publish port 8080 directly to the internet.
2. Enforce authentication and rate limiting at the gateway. The service has neither, because they were out of scope for the exercise. See the [API contract strategy](plan-thought-process.md#api-contract-strategy-for-consumers) for how a public contract, authentication, and sandbox would be added.
3. Allow only `GET /v1/companies/{id}` and `/healthz` through the gateway. The service serves no other routes.

## Step 8: Verify the deployment

Run the same four cases the live tests use against the public address, replacing the host:

```bash
for id in 1 2 0 3; do
  curl --silent --write-out ' [%{http_code}]\n' "https://<your-host>/v1/companies/$id"
done
curl --silent --write-out ' [%{http_code}]\n' https://<your-host>/v1/companies/abc
```

Expect these results:

* Ids 1 and 2 return 200 with the company JSON.
* Ids 0 and 3 return 404 with the `company_not_found` error.
* `abc` returns 400 with the `invalid_id` error.
* `/openapi/v1.json` returns 404, because the generated document is served only in Development.

If every call returns 502, the host cannot reach the upstream. Check outbound network rules and `Upstream__BaseUrl`.

## Step 9: Observe the service

* Collect standard output with the platform's log agent. Upstream failures are logged as warnings, and unexpected exceptions are logged as errors, each with the exception details that are never returned to callers.
* Alert on the rate of 5xx responses, split by status: a rise in 502 or 504 means the upstream is unavailable or slow, while a rise in 500 means a defect in the service.
* Add tracing and metrics with OpenTelemetry when you have a backend for them. The packages, wiring, and prerequisites are in the [OpenTelemetry notes](plan-thought-process.md#opentelemetry-integration).

## Step 10: Scale and protect against upstream problems

* Run at least two replicas so a restart or node failure does not cause an outage. The service is stateless, so scale out on CPU or request rate.
* The upstream is a static file on GitHub. Its availability becomes the availability of this service. Retries, circuit breaking, and short-lived caching are not implemented, and each would reduce the impact of a brief upstream failure. They are described as production options in the plan.
* Keep `Upstream__TimeoutSeconds` below the timeout of the gateway or load balancer in front of the service, so callers receive the service's 504 and not a generic gateway error.

## Step 11: Release and roll back

1. Deploy a new version by changing the image tag. Use a rolling update so old instances serve traffic until new ones pass their probes.
2. Roll back by redeploying the previous tag. Because tags are immutable and the service is stateless, a rollback is a normal deployment.
3. Contract changes follow the versioning rules in the [implementation plan](implementation-plan.md): additive changes stay in `/v1`, and breaking changes need a new major version.

## Example: Azure Container Apps

This example deploys the service to Azure Container Apps with external HTTPS ingress. Replace the values in angle brackets. Confirm the options against the current Azure CLI documentation before relying on them.

1. Sign in and choose the subscription:

   ```bash
   az login
   az account set --subscription <subscription-id>
   ```

2. Create the resource group, then build the image and deploy it from the repository root. `az containerapp up` builds the image from the `Dockerfile`, stores it in a registry, and creates the Container Apps environment and the app:

   ```bash
   az group create --name <resource-group> --location <region>

   az containerapp up \
     --name company-api \
     --resource-group <resource-group> \
     --location <region> \
     --source . \
     --ingress external \
     --target-port 8080
   ```

3. Set the replica range and the upstream settings:

   ```bash
   az containerapp update \
     --name company-api \
     --resource-group <resource-group> \
     --min-replicas 2 \
     --max-replicas 5 \
     --set-env-vars Upstream__TimeoutSeconds=10
   ```

4. Get the public address and run the checks from step 8:

   ```bash
   az containerapp show \
     --name company-api \
     --resource-group <resource-group> \
     --query properties.configuration.ingress.fqdn \
     --output tsv
   ```

5. Add the health probes (step 6) through the app's YAML definition, which `az containerapp update --yaml` applies. For an ongoing deployment, replace `az containerapp up` with a pipeline that builds a versioned image, pushes it to Azure Container Registry, and updates the app to that tag.

## Production checklist

* The commit has a passing CI run, and the image has an immutable version tag.
* The image has been scanned and pushed to the registry.
* Configuration is supplied through environment variables, with no secrets in the image.
* The container runs as non-root with a read-only filesystem and dropped capabilities.
* Startup, liveness, and readiness probes use `/healthz`.
* TLS, authentication, and rate limiting are enforced in front of the service.
* At least two replicas run, and resource limits are set.
* The four verification cases pass against the public address.
* Logs are collected, and alerts exist for 5xx rates.
