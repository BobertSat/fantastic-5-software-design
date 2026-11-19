namespace EuroGlobe.Contracts.Interfaces;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event) 
        where TEvent : class;
    void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) 
        where TEvent : class;
}