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
                { ":aid", new AttributeValue { S = aggregateId.AsString() } },
                { ":seq_r", new AttributeValue { N = "0" } }
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
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                { "#aid", "aid" },
                { "#seq_nr", "seq_nr" }
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":aid", new AttributeValue { S = aggregateId.AsString() } },
                { ":seq_nr", new AttributeValue { N = sequenceNumber.ToString() } }
            }
        };
        var response = await _dynamoDbClient.QueryAsync(request);
        return response.Items.Select(element => element!["payload"].B.GetBuffer())
            .Select(payloadBytes => _eventSerializer.Deserialize(payloadBytes)).ToList();
    }

    private Put GeneratePutJournalRequest(TE @event)
    {
        var pkey = _keyResolver.ResolvePartitionKey(@event.AggregateId, _shardCount);
        var skey = _keyResolver.ResolveSortKey(@event.AggregateId, @event.SequenceNumber);
        var aid = @event.AggregateId.AsString();
        var sequenceNumber = @event.SequenceNumber;
        var payload = _eventSerializer.Serialize(@event);
        var occurredAt = @event.OccurredAt.Millisecond;

        var put = new Put
        {
            TableName = _journalTableName,
            Item = new Dictionary<string, AttributeValue>
            {
                { "pkey", new AttributeValue(pkey) },
                { "skey", new AttributeValue(skey) },
                { "aid", new AttributeValue(@event.AggregateId.AsString()) },
                { "seq_nr", new AttributeValue { N = sequenceNumber.ToString() } },
                { "payload", new AttributeValue { B = new MemoryStream(payload) } },
                { "occurred_at", new AttributeValue { N = occurredAt.ToString() } }
            }
        };
        return put;
    }

    private Put GeneratePutSnapshotRequest([NotNull] TE @event, long sequenceNumber, TA aggregate)
    {
        var pkey = _keyResolver.ResolvePartitionKey(@event.AggregateId, _shardCount);
        var skey = _keyResolver.ResolveSortKey(@event.AggregateId, sequenceNumber);
        var payload = _snapshotSerializer.Serialize(aggregate);
        var put = new Put
        {
            TableName = _snapshotTableName,
            Item = new Dictionary<string, AttributeValue>
            {
                { "pkey", new AttributeValue(pkey) },
                { "skey", new AttributeValue(skey) },
                { "payload", new AttributeValue { B = new MemoryStream(payload) } },
                { "aid", new AttributeValue(@event.AggregateId.AsString()) },
                { "seq_nr", new AttributeValue { N = sequenceNumber.ToString() } },
                { "version", new AttributeValue { N = "1" } },
                { "ttl", new AttributeValue { N = "0" } },
                { "last_updated_at", new AttributeValue() { N = @event.OccurredAt.Millisecond.ToString() } }
            },
            ConditionExpression = "attribute_not_exists(pkey) AND attribute_not_exists(skey)"
        };
        return put;
    }

    private Update GenerateUpdateSnapshotRequest([NotNull] TE @event, long sequenceNumber, long version, TA? aggregate)
    {
        var pkey = _keyResolver.ResolvePartitionKey(@event.AggregateId, _shardCount);
        var skey = _keyResolver.ResolveSortKey(@event.AggregateId, sequenceNumber);
        var update = new Update
        {
            TableName = _snapshotTableName,
            UpdateExpression = "SET #version=:after_version, #last_updated_at=:last_updated_at",
            Key = new Dictionary<string, AttributeValue>
            {
                { "pkey", new AttributeValue(pkey) },
                { "skey", new AttributeValue(skey) }
            },
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                { "#version", "version" },
                { "#last_updated_at", "last_updated_at" }
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":before_version", new AttributeValue { N = version.ToString() } },
                { ":after_version", new AttributeValue { N = (version + 1).ToString() } },
                { ":last_updated_at", new AttributeValue { N = @event.OccurredAt.Millisecond.ToString() } }
            },
            ConditionExpression = "#version = :before_version"
        };
        if (aggregate == null) return update;
        var payload = _snapshotSerializer.Serialize(aggregate);
        update.UpdateExpression =
            "SET #payload=:payload, #seq_nr=:seq_nr, #version=:after_version, #last_updated_at=:last_updated_at";
        update.ExpressionAttributeNames = update.ExpressionAttributeNames.Union(new Dictionary<string, string>
        {
            { "#seq_nr", "seq_nr" },
            { "#payload", "payload" }
        }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        update.ExpressionAttributeValues = update.ExpressionAttributeValues.Union(
            new Dictionary<string, AttributeValue>
            {
                {
                    ":seq_nr", new AttributeValue
                    {
                        N = sequenceNumber.ToString()
                    }
                },
                {
                    ":payload",
                    new AttributeValue
                    {
                        B = new MemoryStream(payload)
                    }
                }
            }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return update;
    }

    private Task CreateEventAndSnapshot([NotNull] TE @event, [NotNull] TA aggregate)
    {
        var transactItems = new List<TransactWriteItem>
        {
            new()
            {
                Put = GeneratePutSnapshotRequest(@event, 0, aggregate),
            },
            new()
            {
                Put = GeneratePutJournalRequest(@event),
            }
        };
        var request = new TransactWriteItemsRequest
        {
            TransactItems = transactItems
        };
        // TODO: keepSnapshot
        var result = _dynamoDbClient.TransactWriteItemsAsync(request);
        return result.ContinueWith(_ => { });
    }

    private Task UpdateEventAndSnapshotOpt([NotNull] TE @event, long version, TA? aggregate)
    {
        var transactItems = new List<TransactWriteItem>
        {
            new()
            {
                Update = GenerateUpdateSnapshotRequest(@event, 0, version, aggregate),
            },
            new()
            {
                Put = GeneratePutJournalRequest(@event),
            }
        };
        // TODO: keepSnapshot
        var request = new TransactWriteItemsRequest
        {
            TransactItems = transactItems
        };
        var result = _dynamoDbClient.TransactWriteItemsAsync(request);
        return result.ContinueWith(_ => { });
    }

    public Task PersistEvent([NotNull] TE @event, long version)
    {
        if (@event.IsCreated)
        {
            throw new ArgumentException("@event is created type.");
        }
        return UpdateEventAndSnapshotOpt(@event, version, default);
    }

    public Task PersistEventAndSnapshot([NotNull] TE @event, [NotNull] TA aggregate)
    {
        if (@event.IsCreated)
        {
            return CreateEventAndSnapshot(@event, aggregate);
        } else {
            return UpdateEventAndSnapshotOpt(@event, aggregate.Version, aggregate);
        }
    }
}