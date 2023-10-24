using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Internal;
using Amazon.Runtime;
using EventStoreAdapterDotNet.impl;
using Xunit;
using Xunit.Abstractions;

namespace EventStoreAdapterTest;

public class EventStoreForDynamoDbTest
{
    private readonly ITestOutputHelper _output;

    public EventStoreForDynamoDbTest(ITestOutputHelper output)
    {
        _output = output;
    } 
    
    [Fact]
    public async Task PersistEventAndSnapshot()
    {
        var credentials = new BasicAWSCredentials("x", "x");
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = "http://127.0.0.1:8000"
        };
        var cts = new CancellationTokenSource();
        var client = new AmazonDynamoDBClient(credentials, config);
        try
        {
            cts.CancelAfter(3000);
            await DynamoDBUtils.CreateJournalTableAsync(client, "journal", cts.Token);
            _output.WriteLine("create journal table");
            Assert.False(cts.IsCancellationRequested);

            // var eventStore = new EventStoreForDynamoDb<UserAccountId, UserAccount, UserAccountEvent>(
            //     client,
            //     "journal",
            //     "snapshot",
            //     "journal-aid-index",
            //     "snapshot-aid-index",
            //     32,
            //     new DefaultKeyResolver<UserAccountId>(),
            //     new DefaultEventSerializer<UserAccountId, UserAccountEvent>(),
            //     new DefaultSnapshotSerializer<UserAccountId, UserAccount>()
            // );
            // var userAccountId = new UserAccountId();
            // var (userAccount, createdEvent) = UserAccount.Create(userAccountId, "test");
            // await eventStore.PersistEventAndSnapshot(@createdEvent, userAccount);
            // _output.WriteLine("PersistEventAndSnapshot");
        } catch (Exception ex)
        {
            Assert.Fail(ex.StackTrace);    
        }
        finally
        {
            cts.Dispose();
        }
    } 
}