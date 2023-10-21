using System.Diagnostics.CodeAnalysis;

namespace EventStoreAdapterDotNet.impl;

public class DefaultKeyResolver<TAid> : IKeyResolver<TAid> where TAid : IAggregateId
{
    public string ResolvePartitionKey([NotNull] TAid aggregateId, long shardCount)
    {
        var remainder = Math.Abs(aggregateId.Value.GetHashCode()) % shardCount;
        return $"{aggregateId.TypeName}-{remainder}";
    }

    public string ResolveSortKey([NotNull] TAid aggregateId, long sequenceNumber)
    {
        return $"{aggregateId.TypeName}-{aggregateId.Value}-{sequenceNumber}";
    }
}