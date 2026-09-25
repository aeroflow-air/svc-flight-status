using AeroFlow.FlightStatus.Domain;

namespace AeroFlow.FlightStatus.Models;

public sealed record DeparturesBoardResponse(
    string Airport,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<DepartureBoardEntry> Departures);

public sealed record DepartureBoardEntry(
    string FlightNumber,
    string Destination,
    DateTimeOffset ScheduledDeparture,
    DateTimeOffset EstimatedDeparture,
    string? Gate,
    string Status,
    int DelayMinutes)
{
    public static DepartureBoardEntry From(Flight flight) => new(
        FlightNumber: flight.FlightNumber,
        Destination: flight.Destination.Value,
        ScheduledDeparture: flight.ScheduledDeparture,
        EstimatedDeparture: flight.EstimatedDeparture,
        Gate: flight.Gate.Match<string?>(g => g.Value, () => null),
        Status: flight.Status.ToString(),
        DelayMinutes: FlightQueries.DelayMinutes(flight));
}
