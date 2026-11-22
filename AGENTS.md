# svc-flight-status

Tracks each flight's status (scheduled → boarding → departed → landed, delays, cancellations) and serves the
departures and arrivals boards. Owner: platform squad. Standards: [ADR-0004](https://github.com/aeroflow-air/platform-handbook/blob/main/docs/decisions/0004-functional-style-csharp.md), [coding standards](https://github.com/aeroflow-air/platform-handbook/blob/main/docs/standards/coding-standards.md).

## Where things live
- `Domain/`: pure core. `FlightLifecycle.Apply(flight, event)` returns `Result<Flight>`; `FlightState`, `FlightEvent`,
  `FlightQueries`, value types (`IataCode`, `Gate`) via `Parse`/`Create`, `Errors.cs`, local `Result`/`Option` (BCL only).
- `Messaging/`: `FlightEventsProcessor` → `FlightEventHandler` → lifecycle. `Storage/`: `IFlightStore`, in memory.
- `Controllers/`: HTTP edge. `FlightsController.Problem` maps domain errors to ProblemDetails.

## Non-negotiables (* = enforced by build or CI in this repo)
- `[ApiController]` controllers, not minimal APIs. Errors leave as ProblemDetails 400/404/409 with a `code` extension.
  `POST /api/flights/{n}/events` has no auth policy yet (ADR-0018; baggage-reclaim has the pattern).
- The domain is pure: static functions, `sealed record`s, `Result<T>` with typed errors, `Option<T>` not `null`.
  * `DomainPurityTests`: nothing in `Domain/` uses logging, I/O, HTTP, data access, Microsoft.Extensions/ASP.NET,
  ServiceDefaults, other layers, `TimeProvider` or `DateTime(Offset).Now`; pass `now` in from the edge.
  Review-only: `throw` (only `UnreachableException`), `null`, `Guid.NewGuid`/`Random`.
- * `AddServiceDefaults()` + `MapDefaultEndpoints()`: OTel, request and messaging logs, `/health`, `/alive`
  (`HealthEndpointTests`, `ObservabilityContractTests`). Don't write your own HTTP or messaging log lines.
- * `[LoggerMessage]` only (CA1848/CA2254 are build errors). Business event ids start at 10,000. `Console` isn't
  banned here yet (no RS0030), but don't use it. No PII, secrets or bodies in logs.
- Options use `Configure<T>` with no startup validation yet; new options use `ValidateOnStart()`. Workflows pin
  `aeroflow-workflows` tags (`v0.5.0`, `v0.7.0` for skills), never `@main`, with job `permissions`.

## Events (ADR-0013/0019)
- Consumes `flight-events` (the simulator's, in aeroflow-local). It publishes nothing today. If it ever does, it gets
  its own `flight-status-events` topic, an outbox (ADR-0017) and deterministic event ids, and never writes to `flight-events`.
- Handlers are idempotent: Service Bus redelivers. There is no processed-id store; `FlightLifecycle` rejects an
  illegal or repeated transition, the flight is unchanged and the handler returns `Rejected(error)`, never throws.
  An unknown `FlightEventType` or empty body is dead-lettered as `unreadable`; update this service first.
- Handler and contract tests cover this (`tests/.../Messaging`, `Contracts/flight-events`). * `workload.yaml` declares
  `queue` + `events:` (subscribes `flight-events`/`flight-status`, publishes nothing); `workload.yml` checks it.

## Build, test, docs, skills
`dotnet build -c Release && dotnet test`; domain tests need no mocks. Update `docs/service.md` with behaviour changes.
Plain Markdown in `.claude/skills/<name>/SKILL.md`, pinned in `.agent-skills.yaml`; read each `description`, follow the
Steps. Don't edit `aeroflow-*` folders (* CI drift check; CODEOWNERS). Change them in [agent-skills](https://github.com/aeroflow-air/agent-skills).
