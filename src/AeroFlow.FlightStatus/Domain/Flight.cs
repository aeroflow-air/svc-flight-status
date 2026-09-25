namespace AeroFlow.FlightStatus.Domain;

/// <summary>
/// Immutable snapshot of a flight. Create with <see cref="Schedule"/>; change it only by applying
/// events through <see cref="FlightLifecycle"/>, which returns a new instance.
/// </summary>
public sealed record Flight
{
    public required string FlightNumber { get; init; }
    public required IataCode Origin { get; init; }
    public required IataCode Destination { get; init; }
    public required DateTimeOffset ScheduledDeparture { get; init; }
    public required DateTimeOffset ScheduledArrival { get; init; }
    public required DateTimeOffset EstimatedDeparture { get; init; }
    public required DateTimeOffset EstimatedArrival { get; init; }
    public DateTimeOffset? ActualDeparture { get; init; }
    public DateTimeOffset? ActualArrival { get; init; }
    public Gate? Gate { get; init; }
    public required FlightState Status { get; init; }
    public IataCode? DivertedTo { get; init; }
    public string? CancellationReason { get; init; }

    /// <summary>A new flight in <see cref="FlightState.Scheduled"/> with estimates equal to the schedule.</summary>
    public static Result<Flight> Schedule(
        string flightNumber,
        IataCode origin,
        IataCode destination,
        DateTimeOffset scheduledDeparture,
        DateTimeOffset scheduledArrival)
    {
        if (!IsValidFlightNumber(flightNumber))
        {
            return new InvalidFlightNumber(flightNumber);
        }

        if (origin == destination)
        {
            return new SameOriginAndDestination(origin);
        }

        if (scheduledArrival <= scheduledDeparture)
        {
            return new ArrivalNotAfterDeparture(scheduledDeparture, scheduledArrival);
        }

        return new Flight
        {
            FlightNumber = flightNumber,
            Origin = origin,
            Destination = destination,
            ScheduledDeparture = scheduledDeparture,
            ScheduledArrival = scheduledArrival,
            EstimatedDeparture = scheduledDeparture,
            EstimatedArrival = scheduledArrival,
            Status = FlightState.Scheduled,
        };
    }

    // Two-character airline designator followed by 1-4 digits, e.g. AF204.
    private static bool IsValidFlightNumber(string value) =>
        value is { Length: >= 3 and <= 6 }
        && value[..2].All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c))
        && value[2..].All(char.IsAsciiDigit);
}
