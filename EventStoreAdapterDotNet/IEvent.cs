namespace EventStoreAdapterDotNet;

public interface IEvent<out TAid>
    where TAid : IAggregateId
{
}