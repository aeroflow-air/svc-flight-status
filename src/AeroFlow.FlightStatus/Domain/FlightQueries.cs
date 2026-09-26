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
}
