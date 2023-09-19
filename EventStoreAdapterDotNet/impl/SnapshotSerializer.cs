using System.Diagnostics.CodeAnalysis;

namespace EventStoreAdapterDotNet.impl;

public class SnapshotSerializer<TAid, TA> : ISnapshotSerializer<TAid, TA>
    where TAid : IAggregateId
    where TA : IAggregate<TA, TAid>
{
    [return: NotNull]
    public byte[] Serialize([NotNull] TA aggregate)
    {
        throw new NotImplementedException();
    }

    [return: NotNull]
    public TA Deserialize([NotNull] byte[] bytes, [NotNull] Type clazz)
    {
        throw new NotImplementedException();
    }
}