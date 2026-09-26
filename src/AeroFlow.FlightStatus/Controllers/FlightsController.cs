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
    [HttpGet("ping")]
    [ProducesResponseType(typeof(FlightPingResponse), StatusCodes.Status200OK)]
    public ActionResult<FlightPingResponse> Ping() =>
        Ok(new FlightPingResponse(
            Service: "AeroFlow.FlightStatus",
            Message: "Flight status probe OK",
            UtcNow: time.GetUtcNow()));

    [HttpGet("{flightNumber}")]
    [ProducesResponseType(typeof(FlightResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult<FlightResponse> GetByFlightNumber(string flightNumber) =>
        store.Find(flightNumber)
            .ToResult(() => new FlightNotFound(flightNumber))
            .Match<ActionResult>(flight => Ok(FlightResponse.From(flight)), Problem);

    /// <summary>Departures board for an airport, ordered by estimated departure.</summary>
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

    /// <summary>Applies a lifecycle event to a flight and returns the updated flight.</summary>
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
