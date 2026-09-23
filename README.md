# AeroFlow Flight Status

Thin ASP.NET Core Web API for flight-status lookups in the [AeroFlow Air](https://github.com/aeroflow-air) portfolio. Health checks, structured logging, ProblemDetails, and a stub for OpenTelemetry — without a shared framework package.

## Local run

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet restore
dotnet run --project src/AeroFlow.FlightStatus
```

- API: `http://localhost:8080` (or the port shown in the console)
- Health: `GET /health`
- Flight probe: `GET /api/flights/ping`
- Demo lookup: `GET /api/flights/{flightNumber}` (e.g. `AF204`, `AF881`; unknown numbers return ProblemDetails 404)

```bash
dotnet test
```

## Container

```bash
docker build -t aeroflow-flight-status .
docker run --rm -p 8080:8080 aeroflow-flight-status
```

Then `curl http://localhost:8080/health`.

## What is deliberately not included

- **No Kubernetes** manifests or Helm charts
- **No Dagger** pipelines
- **No Pulumi** (or other IaC frameworks in-repo yet)
- **No heavy shared framework** NuGet — composition stays in `Program.cs` so squads can delete or replace pieces freely

Infrastructure as Bicep/AVM will land under [`infra/`](infra/README.md) later. Platform conventions live in the **platform-handbook**; reusable Actions come from **aeroflow-workflows**.

## CI

This repository calls the reusable workflow in `aeroflow-workflows` (pinned to `@v0.1.0`) via `.github/workflows/ci.yml`, targeting `AeroFlow.FlightStatus.sln`.

## Pointers

| Resource | Purpose |
| --- | --- |
| platform-handbook | Portfolio standards, CLAUDE.md constraints, ADR process |
| aeroflow-workflows | Shared GitHub Actions (dotnet-ci and decisions validation) |
| `infra/` | Placeholder for Bicep/AVM — see `infra/README.md` |

## Licence / ownership

Internal AeroFlow Air service. Public repository under `aeroflow-air`.
