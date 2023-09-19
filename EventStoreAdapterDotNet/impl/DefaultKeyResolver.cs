using System.Diagnostics.CodeAnalysis;

namespace EventStoreAdapterDotNet.impl;

public class DefaultKeyResolver<TAid> : KeyResolver<TAid> where TAid : IAggregateId
{
    public string resolvePartitionKey([NotNull] TAid aggregateId, long shardCount)
    {
        var remainder = Math.Abs(aggregateId.Value.GetHashCode()) % shardCount;
        return $"{aggregateId.TypeName}-{remainder}";
    }

    public string resolveSortKey([NotNull] TAid aggregateId, long sequenceNumber)
    {
        return $"{aggregateId.TypeName}-{aggregateId.Value}-{sequenceNumber}";
    }
}