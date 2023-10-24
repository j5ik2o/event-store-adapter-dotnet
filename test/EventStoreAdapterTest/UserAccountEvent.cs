using EventStoreAdapterDotNet;

namespace EventStoreAdapterTest;

public interface UserAccountEvent : IEvent<UserAccountId>
{
    
}