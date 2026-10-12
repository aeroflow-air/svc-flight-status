using AeroFlow.FlightStatus.Domain;
using AeroFlow.FlightStatus.Storage;
using Microsoft.Extensions.Options;
using DomainEvent = AeroFlow.FlightStatus.Domain.FlightEvent;

namespace AeroFlow.FlightStatus.Messaging;

/// <summary>What happened to a flight event. <see cref="Rejected"/> carries the domain error.</summary>
public abstract record HandleOutcome
{
    private HandleOutcome()
    {
    }

    public sealed record Applied(FlightState Status) : HandleOutcome;

    public sealed record Rejected(Error Error) : HandleOutcome;
}

/// <summary>
/// Maps simulator flight events onto the flight lifecycle: <c>Scheduled</c> adds the flight, the rest are applied
/// through <see cref="FlightLifecycle"/> against the in-memory store the API reads.
/// </summary>
public sealed class FlightEventHandler(IFlightStore store, IOptions<FlightEventsOptions> options)
{
    private readonly FlightEventsOptions _options = options.Value;

    public HandleOutcome Handle(FlightEventMessage evt)
    {
        var result = evt.EventType switch
        {
            FlightEventType.Scheduled => Schedule(evt),
            FlightEventType.Delayed => Apply(evt, new DelayAnnounced(evt.ScheduledDeparture.AddMinutes(evt.DelayMinutes ?? 0))),
            FlightEventType.Boarding => Apply(evt, StartBoarding),
            FlightEventType.Departed => Apply(evt, new Departed(evt.OccurredAt)),
            FlightEventType.Landed => Apply(evt, new Landed(evt.OccurredAt)),
            FlightEventType.Cancelled => Apply(evt, new Cancelled(evt.Reason ?? "Cancelled")),
            _ => throw new System.Diagnostics.UnreachableException($"Unhandled event type {evt.EventType}."),
        };

        return result.Match<HandleOutcome>(f => new HandleOutcome.Applied(f.Status), e => new HandleOutcome.Rejected(e));
    }

    private Result<Flight> Schedule(FlightEventMessage evt) =>
        IataCode.Parse(evt.Origin)
            .Bind(origin => IataCode.Parse(evt.Destination).Bind(destination =>
                Flight.Schedule(
                    evt.FlightNumber,
                    origin,
                    destination,
                    evt.ScheduledDeparture,
                    evt.ScheduledDeparture.AddMinutes(_options.DefaultBlockMinutes))))
            .Bind(store.Add);

    private Result<Flight> Apply(FlightEventMessage evt, DomainEvent domainEvent) =>
        Apply(evt, flight => FlightLifecycle.Apply(flight, domainEvent));

    private Result<Flight> Apply(FlightEventMessage evt, Func<Flight, Result<Flight>> transition) =>
        store.Update(evt.FlightNumber, transition);

    /// <summary>Boarding needs a gate; use the placeholder if gate allocation hasn't told us one.</summary>
    private Result<Flight> StartBoarding(Flight flight) =>
        (flight.Gate is Option<Gate>.Some
            ? (Result<Flight>)flight
            : Gate.Parse(_options.PlaceholderGate).Bind(gate => FlightLifecycle.Apply(flight, new GateAssigned(gate))))
        .Bind(f => FlightLifecycle.Apply(f, new BoardingStarted()));
}
