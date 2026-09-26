using System.Collections.Immutable;
using AeroFlow.FlightStatus.Domain;

namespace AeroFlow.FlightStatus.Storage;

/// <summary>
/// Thread-safe in-memory store: an immutable dictionary swapped with compare-and-swap.
/// Readers always see a consistent snapshot; writers retry on contention.
/// </summary>
public sealed class InMemoryFlightStore : IFlightStore
{
    private ImmutableDictionary<string, Flight> _flights;

    public InMemoryFlightStore(IEnumerable<Flight> seed)
    {
        _flights = seed.ToImmutableDictionary(f => f.FlightNumber, StringComparer.OrdinalIgnoreCase);
    }

    public ImmutableList<Flight> All() => Volatile.Read(ref _flights).Values.ToImmutableList();

    public Option<Flight> Find(string flightNumber) =>
        Volatile.Read(ref _flights).Find(flightNumber);

    public Result<Flight> Update(string flightNumber, Func<Flight, Result<Flight>> transition)
    {
        while (true)
        {
            var snapshot = Volatile.Read(ref _flights);
            if (!snapshot.TryGetValue(flightNumber, out var current))
            {
                return new FlightNotFound(flightNumber);
            }

            var outcome = transition(current);
            if (outcome is not Result<Flight>.Ok ok)
            {
                return outcome;
            }

            var updated = snapshot.SetItem(current.FlightNumber, ok.Value);
            if (ReferenceEquals(Interlocked.CompareExchange(ref _flights, updated, snapshot), snapshot))
            {
                return outcome;
            }
        }
    }
}
