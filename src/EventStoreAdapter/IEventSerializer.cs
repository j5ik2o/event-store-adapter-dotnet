using System.Diagnostics.CodeAnalysis;

namespace EventStoreAdapterDotNet;

public interface IEventSerializer<out TAid, TE>
    where TAid : IAggregateId
    where TE : IEvent<TAid>
{
    byte[] Serialize([NotNull] TE @event);

    [return: NotNull]
    TE Deserialize([NotNull] byte[] bytes);
}