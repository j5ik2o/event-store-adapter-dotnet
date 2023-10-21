using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace EventStoreAdapterDotNet.impl;

public class EventStoreForDynamoDb<TAid, TA, TE> : IEventStore<TAid, TA, TE>
    where TAid : IAggregateId
    where TA : IAggregate<TA, TAid>
    where TE : IEvent<TAid>
{
    [NotNull]
    private readonly AmazonDynamoDBClient _dynamoDbClient;

    private readonly string _journalTableName;
    private readonly string _snapshotTableName;
    private readonly string _journalAidIndexName;
    private readonly string _snapshotAidIndexName;

    private readonly long _shardCount;
    private readonly IKeyResolver<TAid> _keyResolver;

    private readonly IEventSerializer<TAid, TE> _eventSerializer;
    private readonly ISnapshotSerializer<TAid, TA> _snapshotSerializer;

    public EventStoreForDynamoDb([NotNull] AmazonDynamoDBClient dynamoDbClient,
        string journalTableName, string snapshotTableName,
        string journalAidIndexName, string snapshotAidIndexName,
        long shardCount, 
        IKeyResolver<TAid> keyResolver,
        IEventSerializer<TAid, TE> eventSerializer,
        ISnapshotSerializer<TAid, TA> snapshotSerializer)
    {
        _dynamoDbClient = dynamoDbClient;
        _journalTableName = journalTableName;
        _snapshotTableName = snapshotTableName;
        _journalAidIndexName = journalAidIndexName;
        _snapshotAidIndexName = snapshotAidIndexName;
        _shardCount = shardCount;
        _keyResolver = keyResolver;
        _eventSerializer = eventSerializer;
        _snapshotSerializer = snapshotSerializer;
    }

    public async Task<TA?> GetLatestSnapshotById([NotNull] TAid aggregateId)
    {
        var request = new QueryRequest
        {
            TableName = _snapshotTableName,
            IndexName = _snapshotAidIndexName,
            KeyConditionExpression = "#aid = :aid AND #seq_nr = :seq_r",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                { "#aid", "aid" },
                { "#seq_nr", "seq_nr" },
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":aid", new AttributeValue{ S = aggregateId.AsString() }},
                { ":seq_r", new AttributeValue{ N = "0" }}
            },
            Limit = 1,
        };
        var result = await _dynamoDbClient.QueryAsync(request);
        if (result == null || result.Items.Count == 0)
        {
            return default;
        }
        var versionString = result.Items[0]["version"].N!;
        var version = long.Parse(versionString);
        var payloadBytes = result.Items[0]["payload"].B.GetBuffer();
        var payload = _snapshotSerializer.Deserialize(payloadBytes);
        return payload.WithVersion(version);
    }

    public async Task<List<TE>> GetEventsByIdSinceSequenceNumber([NotNull] TAid aggregateId, long sequenceNumber)
    {
        var request = new QueryRequest
        {
            TableName = _journalTableName,
            IndexName = _journalAidIndexName,
            KeyConditionExpression = "#aid = :aid AND #seq_nr >= :seq_nr",
            ExpressionAttributeNames = new Dictionary<string, string> {
                { "#aid", "aid" },
                { "#seq_nr", "seq_nr" }
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                { ":aid", new AttributeValue{ S = aggregateId.AsString() } },
                { ":seq_nr", new AttributeValue{ N = sequenceNumber.ToString() } }
            }
        };
        var response = await _dynamoDbClient.QueryAsync(request);
        return response.Items.Select(element => element!["payload"].B.GetBuffer())
            .Select(payloadBytes => _eventSerializer.Deserialize(payloadBytes)).ToList();
    }

    private Put putSnapshot([NotNull] TE @event, long sequenceNumber, TA aggregate)
    {
        var pkey = _keyResolver.ResolvePartitionKey(@event.AggregateId, _shardCount);
        var skey = _keyResolver.ResolveSortKey(@event.AggregateId, sequenceNumber);
        var payload = _snapshotSerializer.Serialize(aggregate);
        var put = new Put {
            TableName = _snapshotTableName,
            Item = new Dictionary<string, AttributeValue>()
            {
                 { "pkey", new AttributeValue(pkey) },
                 { "skey", new AttributeValue(skey) },
                 { "payload", new AttributeValue { B = new MemoryStream(payload) }},
                 { "aid", new AttributeValue(@event.AggregateId.AsString()) },
                 { "seq_nr", new AttributeValue(sequenceNumber.ToString()) },
                 { "version", new AttributeValue{ N = "1" } },
                 { "ttl", new AttributeValue{ N = "0" } },
                 { "last_updated_at", new AttributeValue(){ N = @event.OccurredAt.Millisecond.ToString() }} 
            },
            ConditionExpression = "attribute_not_exists(pkey) AND attribute_not_exists(skey)"
        };
        return put;
    }

    private Update updateSnapshot([NotNull] TE @event, long sequenceNumber, long version, TA? aggregate)
    {
        var pkey = _keyResolver.ResolvePartitionKey(@event.AggregateId, _shardCount);
        var skey = _keyResolver.ResolveSortKey(@event.AggregateId, sequenceNumber);
        
        return null;
    }

    private Task createEventAndSnapshot([NotNull] TE @event, [NotNull] TA aggregate)
    {
        throw new NotImplementedException();
    } 

    public Task PersistEvent([NotNull] TE @event, long version)
    {
        throw new NotImplementedException();
    }

    public Task PersistEventAndSnapshot([NotNull] TE @event, [NotNull] TA aggregate)
    {
        throw new NotImplementedException();
    }
}