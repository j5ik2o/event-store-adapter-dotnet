using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace EventStoreAdapterTest;

public class DynamoDBUtils
{
   public static Task CreateJournalTableAsync(AmazonDynamoDBClient dynamoDbClient, string tableName, CancellationToken cancellationToken = default (CancellationToken))
   {
      var request = new CreateTableRequest
      {
         TableName = tableName,
         AttributeDefinitions = new List<AttributeDefinition>
         {
            new()
            {
               AttributeName = "pkey",
               AttributeType = ScalarAttributeType.S 
            },
            new()
            {
               AttributeName = "skey",
               AttributeType = ScalarAttributeType.S
            },
            new()
            {
               AttributeName = "aid",
               AttributeType = ScalarAttributeType.S
            },
            new()
            {
               AttributeName = "seq_nr",
               AttributeType = ScalarAttributeType.N
            }
         },
         ProvisionedThroughput = new ProvisionedThroughput
         {
            ReadCapacityUnits = 10,
            WriteCapacityUnits = 5,
         }
      };
      return dynamoDbClient.CreateTableAsync(request, cancellationToken).ContinueWith(_ => { }, cancellationToken); } 
}