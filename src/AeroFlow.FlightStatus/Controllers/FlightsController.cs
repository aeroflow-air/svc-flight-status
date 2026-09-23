using AeroFlow.FlightStatus.Models;
using Microsoft.AspNetCore.Mvc;

namespace AeroFlow.FlightStatus.Controllers;

[ApiController]
[Route("api/flights")]
public sealed class FlightsController : ControllerBase
{
    // Tiny in-memory demo data — replace with real persistence later.
    private static readonly IReadOnlyDictionary<string, FlightStatusSummary> DemoFlights =
        new Dictionary<string, FlightStatusSummary>(StringComparer.OrdinalIgnoreCase)
        {
            ["AF204"] = new("AF204", "LGW", "EDI", "OnTime"),
            ["AF881"] = new("AF881", "EDI", "AMS", "Delayed"),
        };

    /// <summary>Lightweight hello for the flight-status domain — proves the API is up.</summary>
    [HttpGet("ping")]
    [ProducesResponseType(typeof(FlightPingResponse), StatusCodes.Status200OK)]
    public ActionResult<FlightPingResponse> Ping()
    {
        return Ok(new FlightPingResponse(
            Service: "AeroFlow.FlightStatus",
            Message: "Flight status probe OK",
            UtcNow: DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Demo lookup. Unknown flight numbers return ProblemDetails (404) via NotFound(),
    /// showing the golden-path error shape without a custom middleware stack.
    /// </summary>
    [HttpGet("{flightNumber}")]
    [ProducesResponseType(typeof(FlightStatusSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult<FlightStatusSummary> GetByFlightNumber(string flightNumber)
    {
        if (!DemoFlights.TryGetValue(flightNumber, out var flight))
        {
            return NotFound(new ProblemDetails
            {
                Title = "Flight not found",
                Detail = $"No flight exists with number '{flightNumber}'.",
                Status = StatusCodes.Status404NotFound,
                Instance = HttpContext.Request.Path,
            });
        }

        return Ok(flight);
    }
}
