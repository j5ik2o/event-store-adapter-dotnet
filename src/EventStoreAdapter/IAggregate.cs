using System.Diagnostics.CodeAnalysis;

namespace EventStoreAdapterDotNet;

public interface IAggregate<out TThis, out TAid>
    where TThis : IAggregate<TThis, TAid>
    where TAid : IAggregateId
{
    [NotNull] TAid Id { get; }

    long SequenceNumber { get; }

    long Version { get; }

    [return: NotNull]
    TThis WithVersion(long version);
}