using AeroFlow.FlightStatus.Domain;

namespace AeroFlow.FlightStatus.Models;

/// <summary>An airport's arrivals board.</summary>
/// <param name="Airport">IATA code of the airport.</param>
/// <param name="GeneratedAt">When the board was generated (UTC).</param>
/// <param name="Arrivals">Arriving flights, ordered by estimated arrival.</param>
public sealed record ArrivalsBoardResponse(
    string Airport,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ArrivalBoardEntry> Arrivals);

/// <summary>One row on the arrivals board.</summary>
/// <param name="FlightNumber">Flight number.</param>
/// <param name="Origin">IATA code of the origin airport.</param>
/// <param name="ScheduledDeparture">Scheduled departure time from the origin.</param>
/// <param name="ScheduledArrival">Scheduled arrival time.</param>
/// <param name="EstimatedArrival">Current estimated arrival time.</param>
/// <param name="ActualArrival">Actual arrival time, once landed; null before.</param>
/// <param name="Status">Flight status, for example <c>Departed</c>, <c>Landed</c> or <c>Delayed</c>.</param>
/// <param name="DelayMinutes">Minutes between scheduled and estimated (or actual) departure.</param>
public sealed record ArrivalBoardEntry(
    string FlightNumber,
    string Origin,
    DateTimeOffset ScheduledDeparture,
    DateTimeOffset ScheduledArrival,
    DateTimeOffset EstimatedArrival,
    DateTimeOffset? ActualArrival,
    string Status,
    int DelayMinutes)
{
    public static ArrivalBoardEntry From(Flight flight) => new(
        FlightNumber: flight.FlightNumber,
        Origin: flight.Origin.Value,
        ScheduledDeparture: flight.ScheduledDeparture,
        ScheduledArrival: flight.ScheduledArrival,
        EstimatedArrival: flight.EstimatedArrival,
        ActualArrival: flight.ActualArrival.Match<DateTimeOffset?>(t => t, () => null),
        Status: flight.Status.ToString(),
        DelayMinutes: FlightQueries.DelayMinutes(flight));
}
