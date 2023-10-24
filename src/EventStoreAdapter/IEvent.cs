using System.Diagnostics.CodeAnalysis;

namespace EventStoreAdapterDotNet;

public interface IEvent<out TAid>
    where TAid : IAggregateId
{
    string Id { get;  }
    [NotNull] TAid AggregateId { get; }
    
    long SequenceNumber { get;  }

    DateTimeOffset OccurredAt { get; }
    
    bool IsCreated { get; }
}