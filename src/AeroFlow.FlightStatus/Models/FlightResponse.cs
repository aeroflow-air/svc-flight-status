using AeroFlow.FlightStatus.Domain;

namespace AeroFlow.FlightStatus.Models;

/// <summary>
/// API shape for a flight. Kept separate from the domain <see cref="Flight"/> so either can evolve.
/// Domain <see cref="Option{T}"/> values become nullable here, because JSON represents absence as <c>null</c>.
/// </summary>
/// <param name="FlightNumber">Flight number.</param>
/// <param name="Origin">IATA code of the origin airport.</param>
/// <param name="Destination">IATA code of the destination airport.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="ScheduledDeparture">Scheduled departure time.</param>
/// <param name="ScheduledArrival">Scheduled arrival time.</param>
/// <param name="EstimatedDeparture">Current estimated departure time.</param>
/// <param name="EstimatedArrival">Current estimated arrival time.</param>
/// <param name="ActualDeparture">Actual departure time, once departed.</param>
/// <param name="ActualArrival">Actual arrival time, once landed.</param>
/// <param name="Gate">Assigned gate, or null if none yet.</param>
/// <param name="DelayMinutes">Minutes between scheduled and estimated departure.</param>
/// <param name="DivertedTo">IATA code of the diversion airport, if diverted.</param>
/// <param name="CancellationReason">Reason given, if cancelled.</param>
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
        ActualDeparture: flight.ActualDeparture.Match<DateTimeOffset?>(t => t, () => null),
        ActualArrival: flight.ActualArrival.Match<DateTimeOffset?>(t => t, () => null),
        Gate: flight.Gate.Match<string?>(g => g.Value, () => null),
        DelayMinutes: FlightQueries.DelayMinutes(flight),
        DivertedTo: flight.DivertedTo.Match<string?>(a => a.Value, () => null),
        CancellationReason: flight.CancellationReason.Match<string?>(r => r, () => null));
}
