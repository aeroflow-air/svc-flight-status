namespace AeroFlow.FlightStatus.Domain;

/// <summary>Closed set of things that can happen to a flight. Cases are sealed; the base cannot be derived outside this assembly.</summary>
public abstract record FlightEvent
{
    private protected FlightEvent()
    {
    }

    public string Name => GetType().Name;
}

public sealed record DelayAnnounced(DateTimeOffset NewEstimatedDeparture) : FlightEvent;

public sealed record GateAssigned(Gate Gate) : FlightEvent;

public sealed record GateChanged(Gate Gate) : FlightEvent;

public sealed record BoardingStarted : FlightEvent;

public sealed record Departed(DateTimeOffset ActualTime) : FlightEvent;

public sealed record Landed(DateTimeOffset ActualTime) : FlightEvent;

public sealed record Cancelled(string Reason) : FlightEvent;

public sealed record Diverted(IataCode Airport) : FlightEvent;
