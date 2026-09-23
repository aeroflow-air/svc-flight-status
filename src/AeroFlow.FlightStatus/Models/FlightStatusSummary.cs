namespace AeroFlow.FlightStatus.Models;

public sealed record FlightStatusSummary(
    string FlightNumber,
    string Origin,
    string Destination,
    string Status);
