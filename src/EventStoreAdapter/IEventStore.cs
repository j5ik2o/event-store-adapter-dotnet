using System.Diagnostics.CodeAnalysis;

namespace EventStoreAdapterDotNet;

public interface IEventStore<TAid, TA, TE>
    where TAid : IAggregateId
where TA : IAggregate<TA, TAid>
where TE : IEvent<TAid>
{
    Task<TA?> GetLatestSnapshotById([NotNull] TAid aggregateId);

    Task<List<TE>> GetEventsByIdSinceSequenceNumber(
        [NotNull] TAid aggregateId, long sequenceNumber);

    Task PersistEvent([NotNull] TE @event, long version);

    Task PersistEventAndSnapshot([NotNull] TE @event, [NotNull] TA aggregate);
}