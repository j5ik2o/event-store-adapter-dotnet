using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace EventStoreAdapterDotNet.impl;

public class SnapshotSerializer<TAid, TA> : ISnapshotSerializer<TAid, TA>
    where TAid : IAggregateId
    where TA : IAggregate<TA, TAid>
{
    [return: NotNull]
    public byte[] Serialize([NotNull] TA aggregate)
    {
        var jsonString = JsonSerializer.Serialize(aggregate);
        return Encoding.UTF8.GetBytes(jsonString);
    }

    [return: NotNull]
    public TA Deserialize([NotNull] byte[] bytes)
    {
        var jsonString = Encoding.UTF8.GetString(bytes);
        return JsonSerializer.Deserialize<TA>(jsonString) ?? throw new InvalidOperationException();
    }
}