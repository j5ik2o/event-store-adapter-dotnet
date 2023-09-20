using System.Diagnostics.CodeAnalysis;

namespace EventStoreAdapterDotNet;

public interface ISnapshotSerializer<TAid, TA>
    where TAid : IAggregateId
    where TA : IAggregate<TA, TAid>
{
    byte[] Serialize([NotNull] TA aggregate);

    [return: NotNull]
    TA Deserialize([NotNull] byte[] bytes);
}