using System.Diagnostics.CodeAnalysis;

namespace EventStoreAdapterDotNet.impl;

public class EventStoreForDynamoDB<TAid,TA, TE> : IEventStore<TAid,TA, TE>
    where TAid : IAggregateId
    where TA : IAggregate<TA,TAid>
    where TE : IEvent<TAid>
{
    public TA? GetLatestSnapshotById([NotNull] TAid aggregateId)
    {
        throw new NotImplementedException();
    }

    public List<TE> GetEventsByIdSinceSequenceNumber([NotNull] TAid aggregateId, long sequenceNumber)
    {
        throw new NotImplementedException();
    }

    public void PersistEvent([NotNull] TE @event, long version)
    {
        throw new NotImplementedException();
    }

    public void PersistEventAndSnapshot([NotNull] TE @event, TA aggregate)
    {
        throw new NotImplementedException();
    }
}