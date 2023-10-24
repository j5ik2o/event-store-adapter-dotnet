using EventStoreAdapterDotNet;

namespace EventStoreAdapterTest;

public class UserAccountId : IAggregateId
{
    public UserAccountId(string value)
    {
        Value = value;
        TypeName = "user-account";
    }

    public UserAccountId()
    {
        Value = new Ulid().ToString()!;
        TypeName = "user-account";
    }
    
    public string TypeName { get; }

    public string Value { get; }

    public string AsString()
    {
        return $"{TypeName}-{Value}";
    }
}