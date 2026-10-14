using AeroFlow.FlightStatus.Domain;
using AeroFlow.FlightStatus.Models;
using AeroFlow.FlightStatus.Storage;
using Microsoft.AspNetCore.Mvc;

namespace AeroFlow.FlightStatus.Controllers;

/// <summary>
/// HTTP edge for the flight lifecycle. Side effects (clock, store) live here; decisions live in
/// <see cref="FlightLifecycle"/> and <see cref="FlightQueries"/>. Domain errors become ProblemDetails.
/// </summary>
[ApiController]
[Route("api/flights")]
public sealed class FlightsController(IFlightStore store, TimeProvider time) : ControllerBase
{
    /// <summary>Lightweight hello for the flight-status domain — proves the API is up.</summary>
    /// <response code="200">Service name, message and the server's current UTC time.</response>
    [HttpGet("ping")]
    [ProducesResponseType(typeof(FlightPingResponse), StatusCodes.Status200OK)]
    public ActionResult<FlightPingResponse> Ping() =>
        Ok(new FlightPingResponse(
            Service: "AeroFlow.FlightStatus",
            Message: "Flight status probe OK",
            UtcNow: time.GetUtcNow()));

    /// <summary>Gets a single flight by its flight number.</summary>
    /// <param name="flightNumber">Flight number, for example <c>AF204</c>.</param>
    /// <response code="200">The flight.</response>
    /// <response code="404">No flight with that number.</response>
    [HttpGet("{flightNumber}")]
    [ProducesResponseType(typeof(FlightResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult<FlightResponse> GetByFlightNumber(string flightNumber) =>
        store.Find(flightNumber)
            .ToResult(() => new FlightNotFound(flightNumber))
            .Match<ActionResult>(flight => Ok(FlightResponse.From(flight)), Problem);

    /// <summary>Departures board for an airport, ordered by estimated departure.</summary>
    /// <param name="airport">Three-letter IATA airport code, for example <c>LGW</c>.</param>
    /// <response code="200">The departures board.</response>
    /// <response code="400">The airport code is not a valid IATA code.</response>
    [HttpGet("departures/{airport}")]
    [ProducesResponseType(typeof(DeparturesBoardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<DeparturesBoardResponse> GetDepartures(string airport) =>
        IataCode.Parse(airport)
            .Map(code => new DeparturesBoardResponse(
                Airport: code.Value,
                GeneratedAt: time.GetUtcNow(),
                Departures: FlightQueries.DeparturesBoard(store.All(), code).ConvertAll(DepartureBoardEntry.From)))
            .Match<ActionResult>(Ok, Problem);

    /// <summary>Arrivals board for an airport, ordered by estimated arrival.</summary>
    /// <remarks>Landed and cancelled flights are included; diverted flights are not.</remarks>
    /// <param name="airport">Three-letter IATA airport code, for example <c>LHR</c>.</param>
    /// <response code="200">The arrivals board.</response>
    /// <response code="400">The airport code is not a valid IATA code.</response>
    [HttpGet("arrivals/{airport}")]
    [ProducesResponseType(typeof(ArrivalsBoardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<ArrivalsBoardResponse> GetArrivals(string airport) =>
        IataCode.Parse(airport)
            .Map(code => new ArrivalsBoardResponse(
                Airport: code.Value,
                GeneratedAt: time.GetUtcNow(),
                Arrivals: FlightQueries.ArrivalsBoard(store.All(), code).ConvertAll(ArrivalBoardEntry.From)))
            .Match<ActionResult>(Ok, Problem);

    /// <summary>Applies a lifecycle event to a flight and returns the updated flight.</summary>
    /// <remarks>
    /// <c>type</c> is one of <c>delay</c>, <c>gate-assigned</c>, <c>gate-changed</c>, <c>boarding-started</c>,
    /// <c>departed</c>, <c>landed</c>, <c>cancelled</c> or <c>diverted</c>. Only the fields that type needs are read.
    /// </remarks>
    /// <param name="flightNumber">Flight number, for example <c>AF455</c>.</param>
    /// <param name="request">The event to apply.</param>
    /// <response code="200">The flight after the event was applied.</response>
    /// <response code="400">The body is malformed or missing a field the event type needs.</response>
    /// <response code="404">No flight with that number.</response>
    /// <response code="409">The event is not allowed in the flight's current state (for example, a cancelled flight departing).</response>
    [HttpPost("{flightNumber}/events")]
    [ProducesResponseType(typeof(FlightResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public ActionResult<FlightResponse> PostEvent(string flightNumber, FlightEventRequest request) =>
        request.ToDomainEvent()
            .Bind(evt => store.Update(flightNumber, flight => FlightLifecycle.Apply(flight, evt)))
            .Match<ActionResult>(flight => Ok(FlightResponse.From(flight)), Problem);

    private ObjectResult Problem(Error error)
    {
        var (status, title) = error switch
        {
            FlightNotFound => (StatusCodes.Status404NotFound, "Flight not found"),
            ValidationError => (StatusCodes.Status400BadRequest, "Invalid request"),
            RuleViolation => (StatusCodes.Status409Conflict, "Flight event rejected"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error"),
        };

        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext,
            statusCode: status,
            title: title,
            detail: error.Message,
            instance: HttpContext.Request.Path);
        problem.Extensions["code"] = error.Code;

        return new ObjectResult(problem) { StatusCode = status };
    }
}
