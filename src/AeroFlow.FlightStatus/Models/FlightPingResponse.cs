namespace AeroFlow.FlightStatus.Models;

/// <summary>Response from the ping endpoint.</summary>
/// <param name="Service">Name of the service that answered.</param>
/// <param name="Message">Human-readable status message.</param>
/// <param name="UtcNow">The server's current time in UTC.</param>
public sealed record FlightPingResponse(
    string Service,
    string Message,
    DateTimeOffset UtcNow);
