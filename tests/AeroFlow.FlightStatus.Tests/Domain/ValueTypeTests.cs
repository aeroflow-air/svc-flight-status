using AeroFlow.FlightStatus.Domain;
using Xunit;

namespace AeroFlow.FlightStatus.Tests.Domain;

public sealed class ValueTypeTests
{
    [Theory]
    [InlineData("LGW", "LGW")]
    [InlineData("edi", "EDI")]
    [InlineData("Ams", "AMS")]
    public void IataCode_accepts_three_letters_and_normalises_to_upper_case(string input, string expected)
    {
        var code = AssertResult.Ok(IataCode.Parse(input));

        Assert.Equal(expected, code.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("LG")]
    [InlineData("LGWX")]
    [InlineData("L1W")]
    [InlineData(" LG")]
    public void IataCode_rejects_anything_but_three_letters(string? input)
    {
        var result = IataCode.Parse(input);

        var error = Assert.IsType<InvalidAirportCode>(Assert.IsType<Result<IataCode>.Failure>(result).Error);
        Assert.Equal("invalid_airport_code", error.Code);
    }

    [Fact]
    public void IataCodes_with_the_same_value_are_equal()
    {
        Assert.Equal(TestFlights.Airport("lgw"), TestFlights.Airport("LGW"));
    }

    [Theory]
    [InlineData("12", "12")]
    [InlineData(" 55a ", "55A")]
    public void Gate_accepts_short_alphanumeric_values(string input, string expected)
    {
        Assert.Equal(expected, AssertResult.Ok(Gate.Parse(input)).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("12345")]
    [InlineData("1-2")]
    public void Gate_rejects_blank_long_or_punctuated_values(string? input)
    {
        Assert.IsType<InvalidGate>(Assert.IsType<Result<Gate>.Failure>(Gate.Parse(input)).Error);
    }

    [Fact]
    public void Schedule_creates_a_scheduled_flight_with_estimates_equal_to_the_schedule()
    {
        var flight = TestFlights.Scheduled();

        Assert.Equal(FlightState.Scheduled, flight.Status);
        Assert.Equal(flight.ScheduledDeparture, flight.EstimatedDeparture);
        Assert.Equal(flight.ScheduledArrival, flight.EstimatedArrival);
        Assert.Equal(Option.None<Gate>(), flight.Gate);
    }

    [Fact]
    public void Schedule_rejects_same_origin_and_destination()
    {
        var lgw = TestFlights.Airport("LGW");

        var result = Flight.Schedule("AF204", lgw, lgw, TestFlights.Departure, TestFlights.Arrival);

        AssertResult.Failure<SameOriginAndDestination>(result);
    }

    [Fact]
    public void Schedule_rejects_arrival_not_after_departure()
    {
        var result = Flight.Schedule("AF204", TestFlights.Airport("LGW"), TestFlights.Airport("EDI"), TestFlights.Departure, TestFlights.Departure);

        AssertResult.Failure<ArrivalNotAfterDeparture>(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AF")]
    [InlineData("af204")]
    [InlineData("AF20456")]
    public void Schedule_rejects_malformed_flight_numbers(string number)
    {
        var result = Flight.Schedule(number, TestFlights.Airport("LGW"), TestFlights.Airport("EDI"), TestFlights.Departure, TestFlights.Arrival);

        AssertResult.Failure<InvalidFlightNumber>(result);
    }
}
