namespace AeroFlow.FlightStatus.Domain;

/// <summary>
/// Typed failure with a stable machine-readable <see cref="Code"/> and a human message.
/// Three families, which the HTTP edge maps to 400 / 404 / 409 respectively.
/// </summary>
public abstract record Error(string Code, string Message);

/// <summary>Input was malformed or failed a value rule (maps to 400).</summary>
public abstract record ValidationError(string Code, string Message) : Error(Code, Message);

/// <summary>Input was well formed but breaks a flight lifecycle rule (maps to 409).</summary>
public abstract record RuleViolation(string Code, string Message) : Error(Code, Message);

public sealed record FlightNotFound(string FlightNumber)
    : Error("flight_not_found", $"No flight exists with number '{FlightNumber}'.");

// --- Validation -----------------------------------------------------------------

public sealed record InvalidAirportCode(string? Value)
    : ValidationError("invalid_airport_code", $"'{Value}' is not a valid IATA airport code (expected three letters).");

public sealed record InvalidGate(string? Value)
    : ValidationError("invalid_gate", $"'{Value}' is not a valid gate (expected 1-4 letters or digits).");

public sealed record InvalidFlightNumber(string? Value)
    : ValidationError("invalid_flight_number", $"'{Value}' is not a valid flight number (e.g. AF204).");

public sealed record SameOriginAndDestination(IataCode Airport)
    : ValidationError("same_origin_and_destination", $"Origin and destination are both {Airport}.");

public sealed record ArrivalNotAfterDeparture(DateTimeOffset Departure, DateTimeOffset Arrival)
    : ValidationError("arrival_not_after_departure", $"Scheduled arrival {Arrival:u} must be after scheduled departure {Departure:u}.");

// --- Lifecycle rules ------------------------------------------------------------

public sealed record InvalidTransition(FlightState From, string EventName)
    : RuleViolation("invalid_transition", $"Cannot apply {EventName} to a flight that is {From}.");

public sealed record GateRequired(string EventName)
    : RuleViolation("gate_required", $"{EventName} requires a gate to be assigned first.");

public sealed record GateAlreadyAssigned(Gate Current)
    : RuleViolation("gate_already_assigned", $"Gate {Current} is already assigned; use a gate change instead.");

public sealed record DelayNotLater(DateTimeOffset CurrentEstimate, DateTimeOffset Requested)
    : RuleViolation("delay_not_later", $"New estimated departure {Requested:u} must be later than the current estimate {CurrentEstimate:u}.");

public sealed record LandingBeforeDeparture(DateTimeOffset Departed, DateTimeOffset Landed)
    : RuleViolation("landing_before_departure", $"Landing time {Landed:u} must be after the actual departure {Departed:u}.");

public sealed record DiversionToScheduledDestination(IataCode Destination)
    : RuleViolation("diversion_to_destination", $"Cannot divert to {Destination}; it is already the scheduled destination.");
