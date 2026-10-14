using System.Collections.Immutable;

namespace AeroFlow.FlightStatus.Domain;

/// <summary>Pure read-side projections over flights.</summary>
public static class FlightQueries
{
    /// <summary>
    /// Whole minutes late against the schedule, using the actual departure once known.
    /// Early departures count as zero.
    /// </summary>
    public static int DelayMinutes(Flight flight) =>
        Math.Max(0, (int)Math.Floor((flight.ActualDeparture.Match(actual => actual, () => flight.EstimatedDeparture) - flight.ScheduledDeparture).TotalMinutes));

    /// <summary>
    /// Departures board for <paramref name="airport"/>: flights originating there, ordered by estimated
    /// departure (then flight number). Flights that have already landed or diverted are left off.
    /// </summary>
    public static ImmutableList<Flight> DeparturesBoard(IEnumerable<Flight> flights, IataCode airport) =>
        flights
            .Where(f => f.Origin == airport)
            .Where(f => f.Status is not (FlightState.Landed or FlightState.Diverted))
            .OrderBy(f => f.EstimatedDeparture)
            .ThenBy(f => f.FlightNumber, StringComparer.Ordinal)
            .ToImmutableList();

    /// <summary>
    /// Arrivals board for <paramref name="airport"/>: flights heading there, ordered by estimated arrival (then flight
    /// number). Landed and cancelled flights stay on; diverted flights are left off because they won't arrive here.
    /// </summary>
    public static ImmutableList<Flight> ArrivalsBoard(IEnumerable<Flight> flights, IataCode airport) =>
        flights
            .Where(f => f.Destination == airport)
            .Where(f => f.Status is not FlightState.Diverted)
            .OrderBy(f => f.EstimatedArrival)
            .ThenBy(f => f.FlightNumber, StringComparer.Ordinal)
            .ToImmutableList();
}
