namespace AeroFlow.FlightStatus.Domain;

/// <summary>Three-letter IATA airport code, always stored upper case. Only constructible via <see cref="Parse"/>.</summary>
public sealed record IataCode
{
    private IataCode(string value) => Value = value;

    public string Value { get; }

    /// <summary>Accepts exactly three ASCII letters in any case and normalises to upper case.</summary>
    public static Result<IataCode> Parse(string? value) =>
        value is { Length: 3 } && value.All(char.IsAsciiLetter)
            ? new IataCode(value.ToUpperInvariant())
            : new InvalidAirportCode(value);

    public override string ToString() => Value;
}
