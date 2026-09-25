namespace AeroFlow.FlightStatus.Domain;

/// <summary>
/// Closed set of lifecycle states, exposed as <c>Flight.Status</c> and serialised as a string at the edge.
/// Not called <c>FlightStatus</c> because that would clash with the <c>AeroFlow.FlightStatus</c> root namespace.
/// </summary>
public enum FlightState
{
    Scheduled,
    Delayed,
    Boarding,
    Departed,
    Landed,
    Cancelled,
    Diverted,
}
