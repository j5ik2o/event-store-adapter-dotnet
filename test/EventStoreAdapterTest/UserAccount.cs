using EventStoreAdapterDotNet;

namespace EventStoreAdapterTest;

public class UserAccount : IAggregate<UserAccount, UserAccountId>
{
    public static (UserAccount, UserAccountEvent) Create(UserAccountId id, string name)
    {
        var aggregate = new UserAccount(id, name, 1, 1);
        var eventId = Ulid.NewUlid();
        var created = new UserAccountCreated(eventId.ToString()!, id, name, 1, eventId.Time);
        return (aggregate, created);
    }
    
    private UserAccount(UserAccountId id, string name,  long sequenceNumber, long version)
    {
        Id = id;
        Name = name;
        SequenceNumber = sequenceNumber;
        Version = version;
    }
    public UserAccountId Id { get; }
    
    public string Name { get; }
    public long SequenceNumber { get; }
    public long Version { get; }
    public UserAccount WithVersion(long version)
    {
        return new UserAccount(Id, Name, SequenceNumber, version);
    }
}