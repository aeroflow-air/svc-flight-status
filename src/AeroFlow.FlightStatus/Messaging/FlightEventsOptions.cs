namespace AeroFlow.FlightStatus.Messaging;

/// <summary>Binds the <c>FlightEvents</c> configuration section.</summary>
public sealed class FlightEventsOptions
{
    public const string SectionName = "FlightEvents";

    /// <summary>Aspire connection name; the processor only starts when <c>ConnectionStrings:messaging</c> is set.</summary>
    public const string ConnectionName = "messaging";

    public string Topic { get; set; } = "flight-events";

    public string Subscription { get; set; } = "flight-status";

    /// <summary>The simulator sends no arrival time, so scheduled arrival is departure plus this block time.</summary>
    public int DefaultBlockMinutes { get; set; } = 90;

    /// <summary>
    /// The simulator sends no gate, but the lifecycle requires one before boarding. A flight that reaches
    /// boarding without a gate gets this placeholder until gate allocation publishes real gate events.
    /// </summary>
    public string PlaceholderGate { get; set; } = "TBA";
}
