namespace AeroFlow.FlightStatus.Domain;

/// <summary>Departure gate such as <c>12</c> or <c>55A</c>. Only constructible via <see cref="Parse"/>.</summary>
public sealed record Gate
{
    private Gate(string value) => Value = value;

    public string Value { get; }

    /// <summary>Accepts 1-4 ASCII letters or digits (surrounding whitespace ignored), normalised to upper case.</summary>
    public static Result<Gate> Parse(string? value) =>
        value?.Trim() is { Length: >= 1 and <= 4 } trimmed && trimmed.All(char.IsAsciiLetterOrDigit)
            ? new Gate(trimmed.ToUpperInvariant())
            : new InvalidGate(value);

    public override string ToString() => Value;
}
