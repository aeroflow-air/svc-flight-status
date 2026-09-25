using AeroFlow.FlightStatus.Domain;

namespace AeroFlow.FlightStatus.Models;

/// <summary>API shape for a flight. Kept separate from the domain <see cref="Flight"/> so either can evolve.</summary>
public sealed record FlightResponse(
    string FlightNumber,
    string Origin,
    string Destination,
    string Status,
    DateTimeOffset ScheduledDeparture,
    DateTimeOffset ScheduledArrival,
    DateTimeOffset EstimatedDeparture,
    DateTimeOffset EstimatedArrival,
    DateTimeOffset? ActualDeparture,
    DateTimeOffset? ActualArrival,
    string? Gate,
    int DelayMinutes,
    string? DivertedTo,
    string? CancellationReason)
{
    public static FlightResponse From(Flight flight) => new(
        FlightNumber: flight.FlightNumber,
        Origin: flight.Origin.Value,
        Destination: flight.Destination.Value,
        Status: flight.Status.ToString(),
        ScheduledDeparture: flight.ScheduledDeparture,
        ScheduledArrival: flight.ScheduledArrival,
        EstimatedDeparture: flight.EstimatedDeparture,
        EstimatedArrival: flight.EstimatedArrival,
        ActualDeparture: flight.ActualDeparture,
        ActualArrival: flight.ActualArrival,
        Gate: flight.Gate?.Value,
        DelayMinutes: FlightQueries.DelayMinutes(flight),
        DivertedTo: flight.DivertedTo?.Value,
        CancellationReason: flight.CancellationReason);
}
