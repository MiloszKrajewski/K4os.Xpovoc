using System;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace K4os.Xpovoc.Db.Test.Integrations;

public class MongoStorageTest: StorageTestBase
{
	private string ConnectionString => Secret("/secrets/mongo");
		
	private readonly IMongoClient _client;
	private const string CollectionName = "test_jobs";

	public MongoStorageTest() { _client = new MongoClient(ConnectionString); }
		
	private IMongoDatabase CreateDatabase(string schema) => 
		_client.GetDatabase($"test_db_{schema.NotBlank("noname")}");

	protected override IDbJobStorage CreateStorage(string schema) =>
		new MongoJobStorage(
			() => CreateDatabase(schema),
			CollectionName,
			DefaultMongoJobSerializer.Instance);

	protected override void ClearStorage(string schema)
	{
		var db = CreateDatabase(schema);
		var collection = db.GetCollection<BsonDocument>(CollectionName);
		collection.DeleteMany(FilterDefinition<BsonDocument>.Empty);
	}

	protected override int CountJobs(string schema)
	{
		var db = CreateDatabase(schema);
		var collection = db.GetCollection<BsonDocument>(CollectionName);
		var count = collection.CountDocuments(FilterDefinition<BsonDocument>.Empty);
		return (int) count;
	}
}