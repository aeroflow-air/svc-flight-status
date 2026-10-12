using System.Collections.Immutable;
using AeroFlow.FlightStatus.Domain;

namespace AeroFlow.FlightStatus.Storage;

/// <summary>Edge adapter for flight persistence. The domain never sees this.</summary>
public interface IFlightStore
{
    ImmutableList<Flight> All();

    Option<Flight> Find(string flightNumber);

    /// <summary>
    /// Adds a newly scheduled flight. An existing flight with the same number is replaced only if it has finished
    /// (landed, cancelled or diverted); otherwise the result is <c>FlightAlreadyActive</c>.
    /// </summary>
    Result<Flight> Add(Flight flight);

    /// <summary>
    /// Atomically replaces the flight with the outcome of <paramref name="transition"/>.
    /// The transition must be pure: it may be re-run if another writer got there first.
    /// </summary>
    Result<Flight> Update(string flightNumber, Func<Flight, Result<Flight>> transition);
}
