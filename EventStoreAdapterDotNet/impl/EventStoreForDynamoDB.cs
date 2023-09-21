using System.Diagnostics.CodeAnalysis;
using Amazon.DynamoDBv2;

namespace EventStoreAdapterDotNet.impl;

public class EventStoreForDynamoDB<TAid,TA, TE> : IEventStore<TAid,TA, TE>
    where TAid : IAggregateId
    where TA : IAggregate<TA,TAid>
    where TE : IEvent<TAid>
{
    [NotNull]
    private readonly AmazonDynamoDBClient _dynamoDbClient;

    private readonly string _journalTableName;
    private readonly string _snapshotTableName;
    private readonly string _journalAidIndexName;
    private readonly string _snapshotAidIndexName;
    
    public EventStoreForDynamoDB([NotNull] AmazonDynamoDBClient dynamoDbClient,
        string journalTableName, string snapshotTableName,
        string journalAidIndexName, string snapshotAidIndexName)
    {
        _dynamoDbClient = dynamoDbClient;
        _journalTableName = journalTableName;
        _snapshotTableName = snapshotTableName;
        _journalAidIndexName = journalAidIndexName;
        _snapshotAidIndexName = snapshotAidIndexName;
    }

    public TA? GetLatestSnapshotById([NotNull] TAid aggregateId)
    {
        throw new NotImplementedException();
    }

    public List<TE> GetEventsByIdSinceSequenceNumber([NotNull] TAid aggregateId, long sequenceNumber)
    {
        throw new NotImplementedException();
    }

    public void PersistEvent([NotNull] TE @event, long version)
    {
        throw new NotImplementedException();
    }

    public void PersistEventAndSnapshot([NotNull] TE @event, TA aggregate)
    {
        throw new NotImplementedException();
    }
}