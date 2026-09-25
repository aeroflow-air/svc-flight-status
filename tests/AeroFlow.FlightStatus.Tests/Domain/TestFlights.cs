using AeroFlow.FlightStatus.Domain;
using Xunit;

namespace AeroFlow.FlightStatus.Tests.Domain;

internal static class TestFlights
{
    public static readonly DateTimeOffset Departure = new(2026, 9, 25, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Arrival = Departure.AddMinutes(80);

    public static IataCode Airport(string code) => AssertResult.Ok(IataCode.Parse(code));

    public static Gate Gate(string gate) => AssertResult.Ok(global::AeroFlow.FlightStatus.Domain.Gate.Parse(gate));

    public static Flight Scheduled(string number = "AF204", string origin = "LGW", string destination = "EDI", DateTimeOffset? departure = null)
    {
        var dep = departure ?? Departure;
        return AssertResult.Ok(Flight.Schedule(number, Airport(origin), Airport(destination), dep, dep.AddMinutes(80)));
    }

    public static Flight WithGate() => AssertResult.Ok(FlightLifecycle.Apply(Scheduled(), new GateAssigned(Gate("12"))));

    public static Flight Boarding() => InStatus(FlightState.Boarding);

    public static Flight Departed() => InStatus(FlightState.Departed);

    /// <summary>Drives a flight into <paramref name="status"/> using real events, so fixtures cannot drift from the rules.</summary>
    public static Flight InStatus(FlightState status) => AssertResult.Ok(FlightLifecycle.Replay(Scheduled(), status switch
    {
        FlightState.Scheduled => [],
        FlightState.Delayed => [new DelayAnnounced(Departure.AddMinutes(30))],
        FlightState.Boarding => [new GateAssigned(Gate("12")), new BoardingStarted()],
        FlightState.Departed => [new GateAssigned(Gate("12")), new BoardingStarted(), new Departed(Departure.AddMinutes(5))],
        FlightState.Landed => [new GateAssigned(Gate("12")), new BoardingStarted(), new Departed(Departure.AddMinutes(5)), new Landed(Arrival)],
        FlightState.Cancelled => [new Cancelled("Weather")],
        FlightState.Diverted => [new GateAssigned(Gate("12")), new BoardingStarted(), new Departed(Departure.AddMinutes(5)), new Diverted(Airport("MAN"))],
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    }));
}

internal static class AssertResult
{
    public static T Ok<T>(Result<T> result) =>
        result.Match(value => value, error => throw new Xunit.Sdk.XunitException($"Expected Ok but got {error}"));

    public static TError Failure<TError>(Result<Flight> result)
        where TError : Error =>
        result.Match(
            flight => throw new Xunit.Sdk.XunitException($"Expected {typeof(TError).Name} but got Ok: {flight}"),
            error => Assert.IsType<TError>(error));
}
