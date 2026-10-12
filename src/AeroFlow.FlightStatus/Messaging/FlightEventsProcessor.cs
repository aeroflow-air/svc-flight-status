using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace AeroFlow.FlightStatus.Messaging;

/// <summary>
/// Hosted service that reads the flight-status subscription on the <c>flight-events</c> topic and hands each
/// event to <see cref="FlightEventHandler"/>. Business rejections (invalid transition, unknown flight) are logged and
/// completed so they are not retried; a body that cannot be read is dead-lettered.
/// </summary>
public sealed partial class FlightEventsProcessor(
    ServiceBusClient client,
    FlightEventHandler handler,
    IOptions<FlightEventsOptions> options,
    ILogger<FlightEventsProcessor> logger) : IHostedService, IAsyncDisposable
{
    // Field needed by the .NET 8 [LoggerMessage] generator (it doesn't read primary-ctor params).
    private readonly ILogger _logger = logger;
    private ServiceBusProcessor? _processor;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var o = options.Value;
        _processor = client.CreateProcessor(o.Topic, o.Subscription, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1,
        });
        _processor.ProcessMessageAsync += OnMessageAsync;
        _processor.ProcessErrorAsync += OnErrorAsync;
        await _processor.StartProcessingAsync(cancellationToken);
        LogStarted(o.Topic, o.Subscription);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor is not null)
        {
            await _processor.DisposeAsync();
        }
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        FlightEventMessage? evt;
        try
        {
            evt = args.Message.Body.ToObjectFromJson<FlightEventMessage>(FlightEventMessage.JsonOptions);
        }
        catch (JsonException ex)
        {
            LogUnreadable(ex, args.Message.MessageId);
            await args.DeadLetterMessageAsync(args.Message, "unreadable", ex.Message, args.CancellationToken);
            return;
        }

        if (evt is null)
        {
            await args.DeadLetterMessageAsync(args.Message, "unreadable", "empty body", args.CancellationToken);
            return;
        }

        switch (handler.Handle(evt))
        {
            case HandleOutcome.Applied a:
                LogApplied(evt.FlightNumber, a.Status, evt.EventType);
                break;
            case HandleOutcome.Rejected r:
                LogRejected(evt.EventType, evt.FlightNumber, r.Error.Code, r.Error.Message);
                break;
        }

        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        LogProcessorError(args.Exception, args.ErrorSource.ToString());
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Listening for flight events on {Topic}/{Subscription}")]
    private partial void LogStarted(string topic, string subscription);

    [LoggerMessage(Level = LogLevel.Information, Message = "{FlightNumber} is now {Status} after {EventType}")]
    private partial void LogApplied(string flightNumber, AeroFlow.FlightStatus.Domain.FlightState status, FlightEventType eventType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{EventType} for {FlightNumber} rejected: {Code} {Message}")]
    private partial void LogRejected(FlightEventType eventType, string flightNumber, string code, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dead-lettered unreadable flight event {MessageId}")]
    private partial void LogUnreadable(Exception ex, string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Service Bus processor error ({Source})")]
    private partial void LogProcessorError(Exception ex, string source);
}
