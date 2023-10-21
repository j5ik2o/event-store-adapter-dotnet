namespace EventStoreAdapterDotNet;

public interface IAggregateId
{
    string TypeName { get; }
    string Value { get; }
    string AsString();
}