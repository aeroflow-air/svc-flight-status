using AeroFlow.FlightStatus.Domain;

namespace AeroFlow.FlightStatus.Models;

/// <summary>An airport's departures board.</summary>
/// <param name="Airport">IATA code of the airport.</param>
/// <param name="GeneratedAt">When the board was generated (UTC).</param>
/// <param name="Departures">Departing flights, ordered by estimated departure.</param>
public sealed record DeparturesBoardResponse(
    string Airport,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<DepartureBoardEntry> Departures);

/// <summary>One row on the departures board.</summary>
/// <param name="FlightNumber">Flight number.</param>
/// <param name="Destination">IATA code of the destination airport.</param>
/// <param name="ScheduledDeparture">Scheduled departure time.</param>
/// <param name="EstimatedDeparture">Current estimated departure time.</param>
/// <param name="Gate">Assigned gate, or null if none yet.</param>
/// <param name="Status">Flight status, for example <c>Scheduled</c>, <c>Boarding</c> or <c>Delayed</c>.</param>
/// <param name="DelayMinutes">Minutes between scheduled and estimated departure.</param>
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
