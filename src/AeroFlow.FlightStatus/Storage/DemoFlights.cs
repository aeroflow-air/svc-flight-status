using System.Collections.Immutable;
using AeroFlow.FlightStatus.Domain;

namespace AeroFlow.FlightStatus.Storage;

/// <summary>
/// Demo seed data, built relative to "now" so the boards look current. Each flight is created with
/// <see cref="Flight.Schedule"/> and brought to its current state by replaying real domain events.
/// </summary>
public static class DemoFlights
{
    public static ImmutableList<Flight> Create(DateTimeOffset now)
    {
        // Round down to five minutes, in UTC, so times read like a real timetable.
        var utc = now.ToUniversalTime();
        var t0 = new DateTimeOffset(utc.Ticks - (utc.Ticks % TimeSpan.FromMinutes(5).Ticks), TimeSpan.Zero);
        TimeSpan Min(int minutes) => TimeSpan.FromMinutes(minutes);

        return
        [
            Build("AF204", "LGW", "EDI", t0 + Min(45), Min(80), [new GateAssigned(Gate("12"))]),
            Build("AF881", "EDI", "AMS", t0 + Min(30), Min(95), [new DelayAnnounced(t0 + Min(70)), new GateAssigned(Gate("4"))]),
            Build("AF310", "LGW", "DUB", t0 + Min(20), Min(75), [new GateAssigned(Gate("21")), new BoardingStarted()]),
            Build("AF455", "LGW", "AMS", t0 + Min(120), Min(70), []),
            Build("AF112", "MAN", "LGW", t0 - Min(50), Min(65), [new GateAssigned(Gate("7")), new BoardingStarted(), new Departed(t0 - Min(45))]),
            Build("AF517", "DUB", "MAN", t0 + Min(60), Min(60), [new Cancelled("Crew availability")]),
            Build("AF629", "AMS", "EDI", t0 - Min(80), Min(100), [new GateAssigned(Gate("D6")), new BoardingStarted(), new Departed(t0 - Min(75)), new Diverted(Airport("MAN"))]),
            Build("AF206", "LGW", "EDI", t0 - Min(150), Min(80), [new GateAssigned(Gate("9")), new BoardingStarted(), new Departed(t0 - Min(145)), new Landed(t0 - Min(62))]),
            Build("AF733", "MAN", "DUB", t0 + Min(90), Min(60), [new GateAssigned(Gate("14"))]),
        ];
    }

    private static Flight Build(string number, string origin, string destination, DateTimeOffset departure, TimeSpan blockTime, FlightEvent[] history) =>
        Flight.Schedule(number, Airport(origin), Airport(destination), departure, departure + blockTime)
            .Bind(flight => FlightLifecycle.Replay(flight, history))
            .Match(flight => flight, error => throw new InvalidOperationException($"Invalid demo flight {number}: {error.Message}"));

    // Seed data is hard-coded, so an invalid value is a programming error rather than a business outcome.
    private static IataCode Airport(string code) =>
        IataCode.Parse(code).Match(c => c, e => throw new InvalidOperationException(e.Message));

    private static Gate Gate(string gate) =>
        Domain.Gate.Parse(gate).Match(g => g, e => throw new InvalidOperationException(e.Message));
}
