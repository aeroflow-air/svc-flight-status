using AeroFlow.FlightStatus.Domain;
using AeroFlow.FlightStatus.Messaging;
using AeroFlow.FlightStatus.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace AeroFlow.FlightStatus.Tests.Messaging;

public sealed class FlightEventHandlerTests
{
    private static readonly DateTimeOffset Departure = new(2026, 10, 12, 18, 0, 0, TimeSpan.Zero);

    private readonly InMemoryFlightStore _store = new([]);
    private readonly FlightEventHandler _handler;

    public FlightEventHandlerTests()
    {
        _handler = new FlightEventHandler(_store, Options.Create(new FlightEventsOptions()));
    }

    [Fact]
    public void Scheduled_adds_the_flight()
    {
        Assert.Equal(new HandleOutcome.Applied(FlightState.Scheduled), _handler.Handle(Event(FlightEventType.Scheduled)));

        var flight = Flight();
        Assert.Equal("LHR", flight.Origin.Value);
        Assert.Equal("AMS", flight.Destination.Value);
        Assert.Equal(Departure, flight.ScheduledDeparture);
        Assert.Equal(Departure.AddMinutes(90), flight.ScheduledArrival);
    }

    [Fact]
    public void Full_journey_ends_landed_with_actual_times()
    {
        var departed = Departure.AddMinutes(5);
        var landed = Departure.AddMinutes(80);

        _handler.Handle(Event(FlightEventType.Scheduled));
        _handler.Handle(Event(FlightEventType.Boarding));
        Assert.Equal(new HandleOutcome.Applied(FlightState.Departed), _handler.Handle(Event(FlightEventType.Departed) with { OccurredAt = departed }));
        Assert.Equal(new HandleOutcome.Applied(FlightState.Landed), _handler.Handle(Event(FlightEventType.Landed) with { OccurredAt = landed }));

        var flight = Flight();
        Assert.Equal(Option.Some(departed), flight.ActualDeparture);
        Assert.Equal(Option.Some(landed), flight.ActualArrival);
    }

    [Fact]
    public void Boarding_without_a_gate_uses_the_placeholder()
    {
        _handler.Handle(Event(FlightEventType.Scheduled));

        Assert.Equal(new HandleOutcome.Applied(FlightState.Boarding), _handler.Handle(Event(FlightEventType.Boarding)));
        Assert.Equal("TBA", Assert.IsType<Option<Gate>.Some>(Flight().Gate).Value.Value);
    }

    [Fact]
    public void Delay_moves_the_estimate()
    {
        _handler.Handle(Event(FlightEventType.Scheduled));

        Assert.Equal(new HandleOutcome.Applied(FlightState.Delayed), _handler.Handle(Event(FlightEventType.Delayed) with { DelayMinutes = 25 }));
        Assert.Equal(Departure.AddMinutes(25), Flight().EstimatedDeparture);
    }

    [Fact]
    public void Cancelled_keeps_the_reason()
    {
        _handler.Handle(Event(FlightEventType.Scheduled));

        _handler.Handle(Event(FlightEventType.Cancelled) with { Reason = "Weather" });

        Assert.Equal(FlightState.Cancelled, Flight().Status);
        Assert.Equal(Option.Some("Weather"), Flight().CancellationReason);
    }

    [Fact]
    public void Event_for_an_unknown_flight_is_rejected()
    {
        var outcome = Assert.IsType<HandleOutcome.Rejected>(_handler.Handle(Event(FlightEventType.Departed)));

        Assert.IsType<FlightNotFound>(outcome.Error);
    }

    [Fact]
    public void Out_of_order_event_is_rejected()
    {
        _handler.Handle(Event(FlightEventType.Scheduled));

        var outcome = Assert.IsType<HandleOutcome.Rejected>(_handler.Handle(Event(FlightEventType.Landed)));

        Assert.IsType<InvalidTransition>(outcome.Error);
    }

    [Fact]
    public void Scheduling_an_active_flight_again_is_rejected()
    {
        _handler.Handle(Event(FlightEventType.Scheduled));

        var outcome = Assert.IsType<HandleOutcome.Rejected>(_handler.Handle(Event(FlightEventType.Scheduled)));

        Assert.IsType<FlightAlreadyActive>(outcome.Error);
    }

    [Fact]
    public void A_finished_flight_number_can_be_reused()
    {
        _handler.Handle(Event(FlightEventType.Scheduled));
        _handler.Handle(Event(FlightEventType.Cancelled));

        Assert.Equal(new HandleOutcome.Applied(FlightState.Scheduled), _handler.Handle(Event(FlightEventType.Scheduled)));
    }

    [Fact]
    public void Invalid_flight_number_is_rejected()
    {
        var outcome = Assert.IsType<HandleOutcome.Rejected>(_handler.Handle(Event(FlightEventType.Scheduled) with { FlightNumber = "EZY1234" }));

        Assert.IsType<InvalidFlightNumber>(outcome.Error);
    }

    [Fact]
    public void Simulator_json_deserialises()
    {
        const string json = """
            {"eventId":"8c6f2f1e-6f7e-4a52-9a43-5b9e0a3c2d11","flightId":"SIM-00001","flightNumber":"BA123",
             "eventType":"Landed","origin":"AMS","destination":"LHR","scheduledDeparture":"2026-10-12T18:00:00+00:00",
             "occurredAt":"2026-10-12T19:20:00+00:00","delayMinutes":null,"reason":null}
            """;

        var evt = System.Text.Json.JsonSerializer.Deserialize<FlightEventMessage>(json, FlightEventMessage.JsonOptions);

        Assert.NotNull(evt);
        Assert.Equal(FlightEventType.Landed, evt.EventType);
    }

    private Flight Flight() => Assert.IsType<Option<Flight>.Some>(_store.Find("BA123")).Value;

    private static FlightEventMessage Event(FlightEventType type) =>
        new(Guid.NewGuid(), "SIM-00001", "BA123", type, "LHR", "AMS", Departure, Departure.AddMinutes(1));
}
