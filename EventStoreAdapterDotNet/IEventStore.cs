using System.Diagnostics.CodeAnalysis;

namespace EventStoreAdapterDotNet;

public interface IEventStore<TAid,TA, TE>
    where TAid : IAggregateId
where TA : IAggregate<TA,TAid>
where TE : IEvent<TAid>
{
    TA? GetLatestSnapshotById([NotNull] TAid aggregateId);
    
    List<TE> GetEventsByIdSinceSequenceNumber(
        [NotNull] TAid aggregateId, long sequenceNumber);
    
    void PersistEvent([NotNull] TE @event, long version);

    void PersistEventAndSnapshot([NotNull] TE @event, [NotNull] TA aggregate);
}