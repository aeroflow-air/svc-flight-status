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
- Flight lookup, departures board and lifecycle events: see [Flight lifecycle](#flight-lifecycle)

```bash
dotnet test
```

## Flight lifecycle

The service models a flight from scheduling to arrival. The domain lives in [`src/AeroFlow.FlightStatus/Domain`](src/AeroFlow.FlightStatus/Domain) and is pure: no clock, storage or HTTP.

- **`Flight`**: an immutable record holding flight number, origin and destination (`IataCode`, three letters), scheduled, estimated and actual times, an optional `Gate`, a `Status` (`FlightState`) and, where relevant, a diversion airport or cancellation reason.
- **States**: `Scheduled`, `Delayed`, `Boarding`, `Departed`, `Landed`, `Cancelled`, `Diverted`.
- **Events**: `DelayAnnounced`, `GateAssigned`, `GateChanged`, `BoardingStarted`, `Departed`, `Landed`, `Cancelled`, `Diverted`. This is a closed set of sealed records.
- **`FlightLifecycle.Apply(flight, event)`** returns a `Result<Flight>`: either the new flight or a typed error with a stable code. `Replay` folds a sequence of events and stops at the first error.
- **Rules** include the following. Boarding needs a gate. A flight departs only from `Boarding`. It lands only after departing, at a time later than its departure. A delay must be later than the current estimate, and it moves the estimated arrival by the same amount. Gates cannot change after departure. A flight can only be diverted once airborne, and not to its scheduled destination. Cancelled and landed flights accept no further events.
- **Queries**: `FlightQueries.DelayMinutes` and `FlightQueries.DeparturesBoard`. The board lists flights from an airport ordered by estimated departure and leaves off landed and diverted flights.

Demo flights around LGW, EDI, AMS, MAN and DUB are seeded in memory at start-up. Their times are relative to the current time, and each is built by replaying real events. State resets when the process restarts.

### Endpoints

| Method | Route | Responses |
| --- | --- | --- |
| `GET` | `/api/flights/ping` | 200 |
| `GET` | `/api/flights/{flightNumber}` | 200, 404 |
| `GET` | `/api/flights/departures/{airport}` | 200, 400 (invalid IATA code) |
| `POST` | `/api/flights/{flightNumber}/events` | 200 (updated flight), 400 (malformed), 404 (unknown flight), 409 (rule broken) |

Errors are `application/problem+json`, and a `code` extension carries the error code (for example `invalid_transition`, `gate_required`, `delay_not_later`).

The event body is flat JSON with a `type` discriminator:

| `type` | Required field |
| --- | --- |
| `delay` | `newEstimatedDeparture` |
| `gate-assigned`, `gate-changed` | `gate` |
| `boarding-started` | none |
| `departed`, `landed` | `actualTime` |
| `cancelled` | `reason` |
| `diverted` | `airport` |

```bash
curl http://localhost:8080/api/flights/AF204
curl http://localhost:8080/api/flights/departures/LGW

curl -X POST http://localhost:8080/api/flights/AF455/events \
  -H 'Content-Type: application/json' \
  -d '{"type":"delay","newEstimatedDeparture":"2026-09-25T12:30:00Z"}'

# 409 with "code": "invalid_transition": AF517 is cancelled
curl -X POST http://localhost:8080/api/flights/AF517/events \
  -H 'Content-Type: application/json' \
  -d '{"type":"boarding-started"}'
```

This service follows [ADR-0004: functional-style C#](https://github.com/aeroflow-air/platform-handbook/blob/main/docs/decisions/0004-functional-style-csharp.md). It uses immutable records, a pure domain core with side effects at the edges (controller, store and `TimeProvider`), and explicit `Result` outcomes rather than exceptions. It uses only the BCL, with no FP library.

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
