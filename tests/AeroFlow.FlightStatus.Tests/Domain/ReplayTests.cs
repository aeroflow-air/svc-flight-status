using AeroFlow.FlightStatus.Domain;
using Xunit;
using static AeroFlow.FlightStatus.Tests.Domain.TestFlights;

namespace AeroFlow.FlightStatus.Tests.Domain;

public sealed class ReplayTests
{
    [Fact]
    public void Replay_applies_events_in_order()
    {
        FlightEvent[] events =
        [
            new GateAssigned(Gate("12")),
            new DelayAnnounced(Departure.AddMinutes(25)),
            new GateChanged(Gate("15")),
            new BoardingStarted(),
            new Departed(Departure.AddMinutes(27)),
            new Landed(Arrival.AddMinutes(20)),
        ];

        var flight = AssertResult.Ok(FlightLifecycle.Replay(Scheduled(), events));

        Assert.Equal(FlightState.Landed, flight.Status);
        Assert.Equal(Gate("15"), flight.Gate);
        Assert.Equal(27, FlightQueries.DelayMinutes(flight));
    }

    [Fact]
    public void Replay_of_no_events_returns_the_initial_flight()
    {
        var initial = Scheduled();

        Assert.Equal(initial, AssertResult.Ok(FlightLifecycle.Replay(initial, [])));
    }

    [Fact]
    public void Replay_stops_at_the_first_error_and_does_not_read_further_events()
    {
        var eventsRead = 0;
        IEnumerable<FlightEvent> Events()
        {
            eventsRead++;
            yield return new GateAssigned(Gate("12"));
            eventsRead++;
            yield return new Departed(Departure); // invalid: not boarding yet
            eventsRead++;
            yield return new BoardingStarted();
        }

        var error = AssertResult.Failure<InvalidTransition>(FlightLifecycle.Replay(Scheduled(), Events()));

        Assert.Equal(nameof(Departed), error.EventName);
        Assert.Equal(2, eventsRead);
    }
}
