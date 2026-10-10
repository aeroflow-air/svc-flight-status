using System.Reflection;
using AeroFlow.FlightStatus.Storage;
using Microsoft.OpenApi;
using Scalar.AspNetCore;


var builder = WebApplication.CreateBuilder(args);

// Structured logging: use the host defaults (console JSON in containers when
// configured via environment). Prefer composing here over a shared logging package.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

// API docs: Swashbuckle generates the OpenAPI document (net8 has no built-in generator; that arrived in .NET 9),
// Scalar renders it at /scalar. Gated by ApiDocs:Enabled, which is true in appsettings.Development.json and
// false by default, so Production never exposes the docs unless someone opts in via config.
var apiDocsEnabled = builder.Configuration.GetValue("ApiDocs:Enabled", false);
if (apiDocsEnabled)
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "AeroFlow Flight Status API",
            Version = "v1",
            Description = "Flight lifecycle and departures board for AeroFlow: look up flights, read an airport's departures, and apply lifecycle events (delay, gate, boarding, departed, landed, cancelled, diverted).",
        });
        var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
        if (File.Exists(xml))
        {
            options.IncludeXmlComments(xml);
        }
    });
}

// Edges: the clock and the store are the only side effects; the domain stays pure.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IFlightStore>(sp =>
    new InMemoryFlightStore(DemoFlights.Create(sp.GetRequiredService<TimeProvider>().GetUtcNow())));

// ---------------------------------------------------------------------------
// OpenTelemetry stub — enable when the squad wires an exporter (OTLP / App Insights).
// Keep this commented until you have somewhere to send traces/metrics; no dead deps.
//
// builder.Services.AddOpenTelemetry()
//     .WithTracing(t => t
//         .AddAspNetCoreInstrumentation()
//         .AddHttpClientInstrumentation()
//         // .AddOtlpExporter()
//     )
//     .WithMetrics(m => m
//         .AddAspNetCoreInstrumentation()
//         .AddRuntimeInstrumentation()
//         // .AddOtlpExporter()
//     );
// ---------------------------------------------------------------------------

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapHealthChecks("/health");
app.MapControllers();

if (apiDocsEnabled)
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.MapScalarApiReference(options => options.WithTitle("AeroFlow Flight Status API"));
}

app.Run();

// Expose for WebApplicationFactory in tests.
public partial class Program;
