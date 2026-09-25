using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace AeroFlow.FlightStatus.Tests.Api;

/// <summary>HTTP edge tests with a fixed clock. Each test gets a fresh host, so store mutations don't leak between tests.</summary>
public sealed class FlightsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);
    private readonly WebApplicationFactory<Program> _factory;

    public FlightsApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now))))
        .CreateClient();

    [Fact]
    public async Task Ping_uses_the_injected_clock()
    {
        using var client = CreateClient();

        using var body = await GetJson(client, "/api/flights/ping");

        Assert.Equal(Now, body.RootElement.GetProperty("utcNow").GetDateTimeOffset());
    }

    [Fact]
    public async Task Get_known_flight_returns_status_as_a_string()
    {
        using var client = CreateClient();

        using var body = await GetJson(client, "/api/flights/af881");

        Assert.Equal("AF881", body.RootElement.GetProperty("flightNumber").GetString());
        Assert.Equal("Delayed", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(40, body.RootElement.GetProperty("delayMinutes").GetInt32());
    }

    [Fact]
    public async Task Departures_board_lists_flights_from_that_airport_in_estimated_departure_order()
    {
        using var client = CreateClient();

        using var body = await GetJson(client, "/api/flights/departures/lgw");

        Assert.Equal("LGW", body.RootElement.GetProperty("airport").GetString());
        var departures = body.RootElement.GetProperty("departures").EnumerateArray().ToList();
        Assert.Equal(["AF310", "AF204", "AF455"], departures.Select(d => d.GetProperty("flightNumber").GetString()));
        var times = departures.Select(d => d.GetProperty("estimatedDeparture").GetDateTimeOffset()).ToList();
        Assert.Equal(times.Order(), times);
    }

    [Fact]
    public async Task Departures_board_for_invalid_airport_returns_400_problem()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/flights/departures/L1W");

        await AssertProblem(response, HttpStatusCode.BadRequest, "invalid_airport_code");
    }

    [Fact]
    public async Task Posting_a_valid_delay_returns_the_updated_flight()
    {
        using var client = CreateClient();
        var newEstimate = Now.AddMinutes(45 + 30); // AF204 is scheduled at now + 45 minutes

        var response = await client.PostAsJsonAsync("/api/flights/AF204/events", new { type = "delay", newEstimatedDeparture = newEstimate });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Delayed", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(newEstimate, body.RootElement.GetProperty("estimatedDeparture").GetDateTimeOffset());
        Assert.Equal(30, body.RootElement.GetProperty("delayMinutes").GetInt32());

        using var reread = await GetJson(client, "/api/flights/AF204");
        Assert.Equal("Delayed", reread.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Posting_an_invalid_transition_returns_409_problem_with_error_code()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/flights/AF517/events", new { type = "boarding-started" });

        await AssertProblem(response, HttpStatusCode.Conflict, "invalid_transition");
    }

    [Fact]
    public async Task Posting_an_unknown_event_type_returns_400_problem()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/flights/AF204/events", new { type = "teleported" });

        await AssertProblem(response, HttpStatusCode.BadRequest, "invalid_request");
    }

    [Fact]
    public async Task Posting_to_an_unknown_flight_returns_404_problem()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/flights/AF999/events", new { type = "boarding-started" });

        await AssertProblem(response, HttpStatusCode.NotFound, "flight_not_found");
    }

    private static async Task<JsonDocument> GetJson(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
