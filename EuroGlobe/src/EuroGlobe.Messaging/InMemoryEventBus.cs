using System.Collections.Concurrent;
using EuroGlobe.Contracts.Interfaces;
using Microsoft.Extensions.Logging;

namespace EuroGlobe.Messaging;

public sealed class InMemoryEventBus(ILogger<InMemoryEventBus>? logger = null) : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly ILogger<InMemoryEventBus>? _logger = logger;

    public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : class
    {
        var list = _handlers.GetOrAdd(typeof(TEvent), _ => []);

        lock (list)
        {
            list.Add(handler);
        }
    }

    public Task PublishAsync<TEvent>(TEvent @event)
        where TEvent : class
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var list)) return Task.CompletedTask;

        Delegate[] snapshot;
        lock (list) snapshot = [.. list];

        foreach (var d in snapshot)
        {
            var handler = (Func<TEvent, CancellationToken, Task>)d;
            _ = Task.Run(() => RunAsync(handler, @event), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    private async Task RunAsync<TEvent>(Func<TEvent, CancellationToken, Task> handler, TEvent @event)
        where TEvent : class
    {
        try
        {
            await handler(@event, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Event handler for {EventType} failed", typeof(TEvent).Name);
        }
    }
}
