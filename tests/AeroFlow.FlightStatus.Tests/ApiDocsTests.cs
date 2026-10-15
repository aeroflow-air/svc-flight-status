using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AeroFlow.FlightStatus.Tests;

/// <summary>The test host runs as Development, where ApiDocs:Enabled is true.</summary>
public sealed class ApiDocsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiDocsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApi_document_lists_every_endpoint()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var docPaths = doc.RootElement.GetProperty("paths");
        Assert.True(docPaths.TryGetProperty("/api/flights/ping", out _), "missing path /api/flights/ping");
        Assert.True(docPaths.TryGetProperty("/api/flights/{flightNumber}", out _), "missing path /api/flights/{flightNumber}");
        Assert.True(docPaths.TryGetProperty("/api/flights/departures/{airport}", out _), "missing path /api/flights/departures/{airport}");
        Assert.True(docPaths.TryGetProperty("/api/flights/arrivals/{airport}", out _), "missing path /api/flights/arrivals/{airport}");
        Assert.True(docPaths.TryGetProperty("/api/flights/{flightNumber}/events", out _), "missing path /api/flights/{flightNumber}/events");
    }

    [Fact]
    public async Task Scalar_ui_is_served()
    {
        var response = await _client.GetAsync("/scalar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
