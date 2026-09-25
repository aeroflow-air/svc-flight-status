using AeroFlow.FlightStatus.Domain;
using Xunit;
using static AeroFlow.FlightStatus.Tests.Domain.TestFlights;

namespace AeroFlow.FlightStatus.Tests.Domain;

public sealed class FlightLifecycleTests
{
    // --- Delay -------------------------------------------------------------------

    [Fact]
    public void Delay_sets_status_and_shifts_estimated_arrival_by_the_same_amount()
    {
        var flight = Scheduled();

        var delayed = AssertResult.Ok(FlightLifecycle.Apply(flight, new DelayAnnounced(Departure.AddMinutes(40))));

        Assert.Equal(FlightState.Delayed, delayed.Status);
        Assert.Equal(Departure.AddMinutes(40), delayed.EstimatedDeparture);
        Assert.Equal(Arrival.AddMinutes(40), delayed.EstimatedArrival);
        Assert.Equal(flight.ScheduledDeparture, delayed.ScheduledDeparture);
    }

    [Fact]
    public void Second_delay_shifts_from_the_current_estimate()
    {
        var once = AssertResult.Ok(FlightLifecycle.Apply(Scheduled(), new DelayAnnounced(Departure.AddMinutes(20))));

        var twice = AssertResult.Ok(FlightLifecycle.Apply(once, new DelayAnnounced(Departure.AddMinutes(50))));

        Assert.Equal(Arrival.AddMinutes(50), twice.EstimatedArrival);
    }

    [Fact]
    public void Delay_during_boarding_puts_the_flight_back_to_delayed()
    {
        var delayed = AssertResult.Ok(FlightLifecycle.Apply(InStatus(FlightState.Boarding), new DelayAnnounced(Departure.AddMinutes(15))));

        Assert.Equal(FlightState.Delayed, delayed.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Delay_must_be_later_than_the_current_estimate(int minutes)
    {
        var result = FlightLifecycle.Apply(Scheduled(), new DelayAnnounced(Departure.AddMinutes(minutes)));

        var error = AssertResult.Failure<DelayNotLater>(result);
        Assert.Equal("delay_not_later", error.Code);
    }

    // --- Gates -------------------------------------------------------------------

    [Fact]
    public void Gate_assigned_sets_the_gate_without_changing_status()
    {
        var flight = AssertResult.Ok(FlightLifecycle.Apply(Scheduled(), new GateAssigned(Gate("12"))));

        Assert.Equal(Gate("12"), flight.Gate);
        Assert.Equal(FlightState.Scheduled, flight.Status);
    }

    [Fact]
    public void Gate_assigned_twice_is_rejected()
    {
        AssertResult.Failure<GateAlreadyAssigned>(FlightLifecycle.Apply(WithGate(), new GateAssigned(Gate("14"))));
    }

    [Fact]
    public void Gate_changed_replaces_the_gate_even_while_boarding()
    {
        var flight = AssertResult.Ok(FlightLifecycle.Apply(Boarding(), new GateChanged(Gate("14"))));

        Assert.Equal(Gate("14"), flight.Gate);
        Assert.Equal(FlightState.Boarding, flight.Status);
    }

    [Fact]
    public void Gate_changed_without_an_assigned_gate_is_rejected()
    {
        AssertResult.Failure<GateRequired>(FlightLifecycle.Apply(Scheduled(), new GateChanged(Gate("14"))));
    }

    [Fact]
    public void Gate_change_after_departure_is_rejected()
    {
        var error = AssertResult.Failure<InvalidTransition>(FlightLifecycle.Apply(Departed(), new GateChanged(Gate("14"))));

        Assert.Equal(FlightState.Departed, error.From);
        Assert.Equal(nameof(GateChanged), error.EventName);
    }

    // --- Boarding / departure / landing ------------------------------------------

    [Fact]
    public void Boarding_starts_when_a_gate_is_assigned()
    {
        var flight = AssertResult.Ok(FlightLifecycle.Apply(WithGate(), new BoardingStarted()));

        Assert.Equal(FlightState.Boarding, flight.Status);
    }

    [Fact]
    public void Boarding_without_a_gate_is_rejected()
    {
        AssertResult.Failure<GateRequired>(FlightLifecycle.Apply(Scheduled(), new BoardingStarted()));
    }

    [Fact]
    public void Departed_records_the_actual_departure_time()
    {
        var at = Departure.AddMinutes(7);

        var flight = AssertResult.Ok(FlightLifecycle.Apply(Boarding(), new Departed(at)));

        Assert.Equal(FlightState.Departed, flight.Status);
        Assert.Equal(at, flight.ActualDeparture);
    }

    [Fact]
    public void Departing_without_boarding_is_rejected()
    {
        AssertResult.Failure<InvalidTransition>(FlightLifecycle.Apply(WithGate(), new Departed(Departure)));
    }

    [Fact]
    public void Landed_records_the_actual_arrival_time()
    {
        var flight = AssertResult.Ok(FlightLifecycle.Apply(Departed(), new Landed(Arrival)));

        Assert.Equal(FlightState.Landed, flight.Status);
        Assert.Equal(Arrival, flight.ActualArrival);
    }

    [Fact]
    public void Landing_before_departing_is_rejected()
    {
        AssertResult.Failure<InvalidTransition>(FlightLifecycle.Apply(Boarding(), new Landed(Arrival)));
    }

    [Fact]
    public void Landing_time_must_be_after_the_actual_departure()
    {
        var departed = Departed();

        AssertResult.Failure<LandingBeforeDeparture>(FlightLifecycle.Apply(departed, new Landed(departed.ActualDeparture!.Value.AddMinutes(-1))));
    }

    // --- Cancellation / diversion ------------------------------------------------

    [Fact]
    public void Cancelled_records_the_reason()
    {
        var flight = AssertResult.Ok(FlightLifecycle.Apply(Scheduled(), new Cancelled("Weather")));

        Assert.Equal(FlightState.Cancelled, flight.Status);
        Assert.Equal("Weather", flight.CancellationReason);
    }

    [Fact]
    public void Cancelling_after_departure_is_rejected()
    {
        AssertResult.Failure<InvalidTransition>(FlightLifecycle.Apply(Departed(), new Cancelled("Too late")));
    }

    [Fact]
    public void Diverted_from_departed_records_the_diversion_airport()
    {
        var flight = AssertResult.Ok(FlightLifecycle.Apply(Departed(), new Diverted(Airport("MAN"))));

        Assert.Equal(FlightState.Diverted, flight.Status);
        Assert.Equal(Airport("MAN"), flight.DivertedTo);
    }

    [Fact]
    public void Diverting_a_flight_that_has_not_departed_is_rejected()
    {
        AssertResult.Failure<InvalidTransition>(FlightLifecycle.Apply(Boarding(), new Diverted(Airport("MAN"))));
    }

    [Fact]
    public void Diverting_to_the_scheduled_destination_is_rejected()
    {
        AssertResult.Failure<DiversionToScheduledDestination>(FlightLifecycle.Apply(Departed(), new Diverted(Airport("EDI"))));
    }

    [Fact]
    public void Diverted_flight_can_land_and_keeps_its_diversion_airport()
    {
        var landed = AssertResult.Ok(FlightLifecycle.Apply(InStatus(FlightState.Diverted), new Landed(Arrival)));

        Assert.Equal(FlightState.Landed, landed.Status);
        Assert.Equal(Airport("MAN"), landed.DivertedTo);
    }

    // --- Terminal states ----------------------------------------------------------

    public static TheoryData<FlightState, FlightEvent> TerminalStateEvents()
    {
        var data = new TheoryData<FlightState, FlightEvent>();
        foreach (var status in new[] { FlightState.Cancelled, FlightState.Landed })
        {
            data.Add(status, new DelayAnnounced(Departure.AddHours(3)));
            data.Add(status, new BoardingStarted());
            data.Add(status, new Departed(Departure.AddHours(3)));
            data.Add(status, new GateChanged(Gate("20")));
            data.Add(status, new Cancelled("Again"));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(TerminalStateEvents))]
    public void Cancelled_and_landed_flights_reject_further_events(FlightState status, FlightEvent evt)
    {
        var flight = InStatus(status) with { Gate = Gate("12") };

        var error = AssertResult.Failure<InvalidTransition>(FlightLifecycle.Apply(flight, evt));

        Assert.Equal(status, error.From);
        Assert.Equal("invalid_transition", error.Code);
    }

    [Fact]
    public void Apply_does_not_modify_the_original_flight()
    {
        var original = Scheduled();
        var snapshot = original with { };

        _ = FlightLifecycle.Apply(original, new DelayAnnounced(Departure.AddMinutes(30)));

        Assert.Equal(snapshot, original);
    }
}
