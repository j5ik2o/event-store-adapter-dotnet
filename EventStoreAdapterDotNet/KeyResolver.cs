using System.Diagnostics.CodeAnalysis;

namespace EventStoreAdapterDotNet;

public interface KeyResolver<TAid> where TAid : IAggregateId
{
    [return: NotNull]
    string resolvePartitionKey([NotNull] TAid aggregateId, long shardCount);

    [return: NotNull]
    string resolveSortKey([NotNull] TAid aggregateId, long sequenceNumber);
}