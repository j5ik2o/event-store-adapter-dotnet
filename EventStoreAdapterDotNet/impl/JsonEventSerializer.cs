using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace EventStoreAdapterDotNet.impl;

public class JsonEventSerializer<TAid, TE> : IEventSerializer<TAid, TE>
    where TAid : IAggregateId
    where TE : IEvent<TAid>
{
    public byte[] Serialize([NotNull] TE @event)
    {
        var jsonString = JsonSerializer.Serialize(@event);
        return Encoding.UTF8.GetBytes(jsonString);
    }

    [return: NotNull]
    public TE Deserialize([NotNull] byte[] bytes)
    {
        var jsonString = Encoding.UTF8.GetString(bytes);
        return JsonSerializer.Deserialize<TE>(jsonString) ?? throw new InvalidOperationException();
    }
}