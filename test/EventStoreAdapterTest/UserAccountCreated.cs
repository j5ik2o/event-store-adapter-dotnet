namespace EventStoreAdapterTest;

public class UserAccountCreated : UserAccountEvent
{
    public UserAccountCreated(string id, UserAccountId aggregateId, string name, long sequenceNumber, DateTimeOffset occurredAt)
    {
        Id = id;
        AggregateId = aggregateId;
        Name = name;
        SequenceNumber = sequenceNumber;
        OccurredAt = occurredAt;
    }
    public string Id { get; }
    public UserAccountId AggregateId { get; }
    public string Name { get; }
    public long SequenceNumber { get; }
    public DateTimeOffset OccurredAt { get; }
    public bool IsCreated => true;
}