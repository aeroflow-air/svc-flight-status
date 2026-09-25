using AeroFlow.FlightStatus.Domain;
using Xunit;
using static AeroFlow.FlightStatus.Tests.Domain.TestFlights;

namespace AeroFlow.FlightStatus.Tests.Domain;

public sealed class FlightQueriesTests
{
    [Fact]
    public void DelayMinutes_is_zero_for_an_on_time_flight()
    {
        Assert.Equal(0, FlightQueries.DelayMinutes(Scheduled()));
    }

    [Fact]
    public void DelayMinutes_follows_the_estimated_departure()
    {
        var delayed = AssertResult.Ok(FlightLifecycle.Apply(Scheduled(), new DelayAnnounced(Departure.AddMinutes(45))));

        Assert.Equal(45, FlightQueries.DelayMinutes(delayed));
    }

    [Fact]
    public void DelayMinutes_uses_the_actual_departure_once_known_and_ignores_early_departures()
    {
        var late = AssertResult.Ok(FlightLifecycle.Apply(Boarding(), new Departed(Departure.AddMinutes(12))));
        var early = AssertResult.Ok(FlightLifecycle.Apply(Boarding(), new Departed(Departure.AddMinutes(-3))));

        Assert.Equal(12, FlightQueries.DelayMinutes(late));
        Assert.Equal(0, FlightQueries.DelayMinutes(early));
    }

    [Fact]
    public void DeparturesBoard_filters_by_origin_and_orders_by_estimated_departure()
    {
        var first = Scheduled("AF100", "LGW", "EDI", Departure);
        var delayedPastOthers = AssertResult.Ok(FlightLifecycle.Apply(
            Scheduled("AF101", "LGW", "AMS", Departure.AddMinutes(10)),
            new DelayAnnounced(Departure.AddMinutes(90))));
        var second = Scheduled("AF102", "LGW", "DUB", Departure.AddMinutes(30));
        var elsewhere = Scheduled("AF103", "MAN", "LGW", Departure.AddMinutes(5));

        var board = FlightQueries.DeparturesBoard([second, elsewhere, delayedPastOthers, first], Airport("LGW"));

        Assert.Equal(["AF100", "AF102", "AF101"], board.Select(f => f.FlightNumber));
    }

    [Fact]
    public void DeparturesBoard_leaves_off_landed_and_diverted_flights_but_shows_cancelled_ones()
    {
        var flights = new[]
        {
            InStatus(FlightState.Landed),
            InStatus(FlightState.Diverted),
            InStatus(FlightState.Cancelled),
            InStatus(FlightState.Departed),
        };

        var board = FlightQueries.DeparturesBoard(flights, Airport("LGW"));

        Assert.Equal([FlightState.Departed, FlightState.Cancelled], board.Select(f => f.Status).Order());
    }
}
