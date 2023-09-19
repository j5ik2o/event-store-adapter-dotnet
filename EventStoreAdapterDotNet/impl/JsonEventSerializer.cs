using System.Diagnostics.CodeAnalysis;

namespace EventStoreAdapterDotNet.impl;

public class JsonEventSerializer<TAid, TE> : IEventSerializer<TAid, TE>
    where TAid : IAggregateId
    where TE : IEvent<TAid>
{
    [return: NotNull]
    public byte[] Serialize([NotNull] TE @event)
    {
        throw new NotImplementedException();
    }
    
    [return: NotNull]
    public TE Deserialize([NotNull] byte[] bytes, [NotNull] Type clazz)
    {
        throw new NotImplementedException();
    }
}