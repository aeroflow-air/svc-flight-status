using AeroFlow.FlightStatus.Domain;

namespace AeroFlow.FlightStatus.Models;

/// <summary>
/// Flat JSON body for <c>POST api/flights/{flightNumber}/events</c>. <see cref="Type"/> is the discriminator;
/// only the fields relevant to that type are read. Mapped to the closed <see cref="FlightEvent"/> union at the edge.
/// </summary>
public sealed record FlightEventRequest
{
    /// <summary>Event type: delay, gate-assigned, gate-changed, boarding-started, departed, landed, cancelled or diverted.</summary>
    public string? Type { get; init; }
    /// <summary>New estimated departure. Required for <c>delay</c>.</summary>
    public DateTimeOffset? NewEstimatedDeparture { get; init; }
    /// <summary>Gate, for example <c>A12</c>. Required for <c>gate-assigned</c> and <c>gate-changed</c>.</summary>
    public string? Gate { get; init; }
    /// <summary>Actual time of the event. Required for <c>departed</c> and <c>landed</c>.</summary>
    public DateTimeOffset? ActualTime { get; init; }
    /// <summary>Cancellation reason. Required for <c>cancelled</c>.</summary>
    public string? Reason { get; init; }
    /// <summary>IATA code of the diversion airport. Required for <c>diverted</c>.</summary>
    public string? Airport { get; init; }

    public static readonly IReadOnlyList<string> KnownTypes =
        ["delay", "gate-assigned", "gate-changed", "boarding-started", "departed", "landed", "cancelled", "diverted"];

    public Result<FlightEvent> ToDomainEvent() => Type switch
    {
        "delay" => Require(NewEstimatedDeparture, "newEstimatedDeparture").Map<FlightEvent>(t => new DelayAnnounced(t)),
        "gate-assigned" => Domain.Gate.Parse(Gate).Map<FlightEvent>(g => new GateAssigned(g)),
        "gate-changed" => Domain.Gate.Parse(Gate).Map<FlightEvent>(g => new GateChanged(g)),
        "boarding-started" => new BoardingStarted(),
        "departed" => Require(ActualTime, "actualTime").Map<FlightEvent>(t => new Departed(t)),
        "landed" => Require(ActualTime, "actualTime").Map<FlightEvent>(t => new Landed(t)),
        "cancelled" => RequireText(Reason, "reason").Map<FlightEvent>(r => new Cancelled(r)),
        "diverted" => IataCode.Parse(Airport).Map<FlightEvent>(a => new Diverted(a)),
        null or "" => new InvalidRequest("'type' is required."),
        _ => new InvalidRequest($"Unknown event type '{Type}'. Expected one of: {string.Join(", ", KnownTypes)}."),
    };

    private static Result<DateTimeOffset> Require(DateTimeOffset? value, string field) =>
        value is { } v ? v : new InvalidRequest($"'{field}' is required for this event type.");

    private static Result<string> RequireText(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? new InvalidRequest($"'{field}' is required for this event type.") : value.Trim();
}

/// <summary>Malformed request body (maps to 400).</summary>
public sealed record InvalidRequest(string Message) : ValidationError("invalid_request", Message);
