using System.Diagnostics;
using static AeroFlow.FlightStatus.Domain.FlightState;

namespace AeroFlow.FlightStatus.Domain;

/// <summary>Pure state transitions for a flight. No clock, no storage, no exceptions for business failures.</summary>
public static class FlightLifecycle
{
    public static Result<Flight> Apply(Flight flight, FlightEvent evt) => evt switch
    {
        DelayAnnounced e => Delay(flight, e),
        GateAssigned e => AssignGate(flight, e),
        GateChanged e => ChangeGate(flight, e),
        BoardingStarted e => StartBoarding(flight, e),
        Departed e => Depart(flight, e),
        Landed e => Land(flight, e),
        Cancelled e => Cancel(flight, e),
        Diverted e => Divert(flight, e),
        _ => throw new UnreachableException($"Unhandled flight event {evt.Name}."),
    };

    /// <summary>Left fold of <see cref="Apply"/> over <paramref name="events"/>; stops at (and returns) the first error.</summary>
    public static Result<Flight> Replay(Flight initial, IEnumerable<FlightEvent> events)
    {
        // A plain loop rather than Aggregate so we genuinely stop enumerating at the first failure.
        Result<Flight> current = initial;
        foreach (var evt in events)
        {
            current = current.Bind(flight => Apply(flight, evt));
            if (current is Result<Flight>.Failure)
            {
                return current;
            }
        }

        return current;
    }

    private static Result<Flight> Delay(Flight f, DelayAnnounced e) => f.Status switch
    {
        Scheduled or Delayed or Boarding when e.NewEstimatedDeparture <= f.EstimatedDeparture =>
            new DelayNotLater(f.EstimatedDeparture, e.NewEstimatedDeparture),
        Scheduled or Delayed or Boarding => f with
        {
            Status = Delayed,
            EstimatedDeparture = e.NewEstimatedDeparture,
            EstimatedArrival = f.EstimatedArrival + (e.NewEstimatedDeparture - f.EstimatedDeparture),
        },
        _ => Invalid(f, e),
    };

    private static Result<Flight> AssignGate(Flight f, GateAssigned e) => f.Status switch
    {
        Scheduled or Delayed or Boarding when f.Gate is { } current => new GateAlreadyAssigned(current),
        Scheduled or Delayed or Boarding => f with { Gate = e.Gate },
        _ => Invalid(f, e),
    };

    private static Result<Flight> ChangeGate(Flight f, GateChanged e) => f.Status switch
    {
        Scheduled or Delayed or Boarding when f.Gate is null => new GateRequired(e.Name),
        Scheduled or Delayed or Boarding => f with { Gate = e.Gate },
        _ => Invalid(f, e),
    };

    private static Result<Flight> StartBoarding(Flight f, BoardingStarted e) => f.Status switch
    {
        Scheduled or Delayed when f.Gate is null => new GateRequired(e.Name),
        Scheduled or Delayed => f with { Status = Boarding },
        _ => Invalid(f, e),
    };

    private static Result<Flight> Depart(Flight f, Departed e) => f.Status switch
    {
        Boarding => f with { Status = FlightState.Departed, ActualDeparture = e.ActualTime },
        _ => Invalid(f, e),
    };

    private static Result<Flight> Land(Flight f, Landed e) => f.Status switch
    {
        FlightState.Departed or FlightState.Diverted when e.ActualTime <= f.ActualDeparture =>
            new LandingBeforeDeparture(f.ActualDeparture.GetValueOrDefault(), e.ActualTime),
        FlightState.Departed or FlightState.Diverted => f with { Status = FlightState.Landed, ActualArrival = e.ActualTime },
        _ => Invalid(f, e),
    };

    private static Result<Flight> Cancel(Flight f, Cancelled e) => f.Status switch
    {
        Scheduled or Delayed or Boarding => f with { Status = FlightState.Cancelled, CancellationReason = e.Reason },
        _ => Invalid(f, e),
    };

    private static Result<Flight> Divert(Flight f, Diverted e) => f.Status switch
    {
        FlightState.Departed when e.Airport == f.Destination => new DiversionToScheduledDestination(f.Destination),
        FlightState.Departed => f with { Status = FlightState.Diverted, DivertedTo = e.Airport },
        _ => Invalid(f, e),
    };

    private static InvalidTransition Invalid(Flight f, FlightEvent e) => new(f.Status, e.Name);
}
