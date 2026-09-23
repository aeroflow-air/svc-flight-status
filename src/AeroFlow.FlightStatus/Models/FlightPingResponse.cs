namespace AeroFlow.FlightStatus.Models;

public sealed record FlightPingResponse(
    string Service,
    string Message,
    DateTimeOffset UtcNow);
