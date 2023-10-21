using System.Diagnostics.CodeAnalysis;

namespace EventStoreAdapterDotNet;

public interface IKeyResolver<in TAid> where TAid : IAggregateId
{
    [return: NotNull]
    string ResolvePartitionKey([NotNull] TAid aggregateId, long shardCount);

    [return: NotNull]
    string ResolveSortKey([NotNull] TAid aggregateId, long sequenceNumber);
}