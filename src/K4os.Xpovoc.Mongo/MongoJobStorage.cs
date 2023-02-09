using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.Mongo.Model;
using MongoDB.Bson;
using MongoDB.Driver;

namespace K4os.Xpovoc.Mongo;

public class MongoJobStorage: IDbJobStorage
{
	public const string DefaultConnectionString = "mongodb://localhost";
	public const string DefaultDatabaseName = "xpovoc";
	public const string DefaultCollectionName = "jobs";

	private const string CompositeIndexName = "status_1_invisible_until_1_scheduled_for_1";

	private static readonly FindOneAndUpdateOptions<JobDocument> ClaimOptions = new() {
		Hint = CompositeIndexName,
		ReturnDocument = ReturnDocument.After,
		IsUpsert = false,
	};

	private static readonly FindOneAndUpdateOptions<JobDocument> UpdateOptions = new() {
		ReturnDocument = ReturnDocument.After,
		IsUpsert = false,
	};

	private const int PruneLimit = 100;

	private static readonly FindOptions FindObsoleteOptions = new() {
		Hint = CompositeIndexName,
	};
		
	private static readonly ProjectionDefinition<JobDocument, ObjectId> DocumentIdProjection =
		Builders<JobDocument>.Projection.Expression(d => d.RowId);

	private static readonly FilterDefinitionBuilder<JobDocument> JobFilter =
		Builders<JobDocument>.Filter;

	private static readonly UpdateDefinitionBuilder<JobDocument> JobUpdate =
		Builders<JobDocument>.Update;

	private static readonly IndexKeysDefinitionBuilder<JobDocument> JobIndexes =
		Builders<JobDocument>.IndexKeys;

	private readonly Lazy<Task<IMongoCollection<JobDocument>>> _collection;
	private readonly IMongoJobSerializer _serializer;

	public MongoJobStorage(
		IMongoJobStorageConfig configuration,
		IMongoJobSerializer serializer):
		this(
			() => CreateDatabase(configuration),
			configuration.CollectionName,
			serializer) { }

	public MongoJobStorage(
		IMongoJobSerializer serializer):
		this(new MongoJobStorageConfig(), serializer) { }

	public MongoJobStorage(
		Func<IMongoDatabase>? databaseFactory = null,
		string? collectionName = null,
		IMongoJobSerializer? serializer = null)
	{
		_serializer = serializer ?? DefaultMongoJobSerializer.Instance;
		_collection = new Lazy<Task<IMongoCollection<JobDocument>>>(
			() => Initialize(databaseFactory, collectionName),
			LazyThreadSafetyMode.ExecutionAndPublication);
	}

	private static IMongoDatabase CreateDatabase(
		string? connectionString, string? databaseName)
	{
		var client = new MongoClient(connectionString ?? DefaultConnectionString);
		return client.GetDatabase(databaseName ?? DefaultDatabaseName);
	}

	private static IMongoDatabase CreateDatabase(
		IMongoJobStorageConfig configuration) =>
		CreateDatabase(configuration.ConnectionString, configuration.DatabaseName);

	private static IMongoDatabase CreateDefaultDatabase() =>
		CreateDatabase(null, null);

	private static async Task<IMongoCollection<JobDocument>> Initialize(
		Func<IMongoDatabase>? databaseFactory, string? collectionName)
	{
		var database = (databaseFactory ?? CreateDefaultDatabase)();
		var collection = database.GetCollection<JobDocument>(
			collectionName ?? DefaultCollectionName);

		await collection.Indexes.CreateManyAsync(
			new[] {
				new CreateIndexModel<JobDocument>(
					JobIndexes
						.Hashed(d => d.JobId)),
				new CreateIndexModel<JobDocument>(
					JobIndexes
						.Ascending(d => d.Status)
						.Ascending(d => d.InvisibleUntil)
						.Ascending(d => d.ScheduledFor)),
			});

		return collection;
	}

	private Task<IMongoCollection<JobDocument>> Jobs => _collection.Value;

	private BsonValue Serialize(object payload) => _serializer.Serialize(payload);
	private object Deserialize(BsonValue payload) => _serializer.Deserialize(payload);

	private async Task Insert(CancellationToken token, JobDocument job) =>
		await (await Jobs).InsertOneAsync(job, cancellationToken: token);

	private async Task<JobDocument?> Update(
		CancellationToken token,
		FilterDefinition<JobDocument> filter,
		UpdateDefinition<JobDocument> update,
		FindOneAndUpdateOptions<JobDocument>? options) =>
		await (await Jobs).FindOneAndUpdateAsync(
			filter, update, options.SharedNotNull(), token);

	private async Task<long> Prune(
		CancellationToken token,
		FilterDefinition<JobDocument> filter,
		DeleteOptions? options) =>
		(await (await Jobs).DeleteManyAsync(filter, options.SharedNotNull(), token)).DeletedCount;

	public async Task<IDbJob?> Claim(
		CancellationToken token, Guid worker, DateTime now, DateTime until)
	{
		var filter = JobFilter.And(
			JobFilter.Eq(x => x.Status, JobStatus.Ready),
			JobFilter.Lte(x => x.InvisibleUntil, now.ToUtc()),
			JobFilter.Eq(x => x.Protocol, JobDocument.CurrentProtocol));

		var update = JobUpdate
			.Inc(x => x.Attempt, 1)
			.Set(x => x.ClaimedBy, worker)
			.Set(x => x.InvisibleUntil, until.ToUtc());

		var document = await Update(token, filter, update, ClaimOptions);

		return document switch {
			{ Payload: var payload } => new DbJobWrapper(document, Deserialize(payload)),
			null => null,
		};
	}

	public async Task<bool> KeepClaim(
		CancellationToken token, Guid worker, IDbJob job, DateTime until)
	{
		var context = AsDocument(job).Required();

		var filter = JobFilter.And(
			JobFilter.Eq(x => x.RowId, context.RowId),
			JobFilter.Eq(x => x.ClaimedBy, worker));

		var update = JobUpdate
			.Set(x => x.InvisibleUntil, until.ToUtc());

		return await Update(token, filter, update, UpdateOptions) is not null;
	}

	private static JobDocument? AsDocument(IJob job) => job.Context as JobDocument;

	public Task Complete(Guid worker, IDbJob job, DateTime now) =>
		UpdateStatus(worker, job, DateTime.MaxValue, JobStatus.Completed);

	public Task Forget(Guid worker, IDbJob job, DateTime now) =>
		UpdateStatus(worker, job, DateTime.MaxValue, JobStatus.Failed);

	public Task Retry(Guid worker, IDbJob job, DateTime when) =>
		UpdateStatus(worker, job, when, JobStatus.Ready);

	private async Task UpdateStatus(Guid worker, IJob job, DateTime until, JobStatus status)
	{
		var context = AsDocument(job).Required();

		var filter = JobFilter.And(
			JobFilter.Eq(x => x.RowId, context.RowId),
			JobFilter.Eq(x => x.ClaimedBy, worker));

		var update = JobUpdate
			.Set(x => x.ClaimedBy, null)
			.Set(x => x.InvisibleUntil, until.ToUtc())
			.Set(x => x.Status, status);

		await Update(CancellationToken.None, filter, update, UpdateOptions);
	}

	public async Task<Guid> Schedule(object payload, DateTime when)
	{
		var job = new JobDocument {
			RowId = ObjectId.GenerateNewId(),
			JobId = Guid.NewGuid(),
			ScheduledFor = when,
			InvisibleUntil = when,
			Status = JobStatus.Ready,
			Attempt = 0,
			ClaimedBy = null,
			Payload = Serialize(payload),
		};
		await Insert(CancellationToken.None, job);
		return job.JobId;
	}

	public async Task<bool> Prune(DateTime cutoff)
	{
		var outdatedJobs = await FindOutdated(cutoff);
		if (outdatedJobs?.Count <= 0) return false;

		var deleteFilter = JobFilter.In(d => d.RowId, outdatedJobs);
		return await Prune(CancellationToken.None, deleteFilter, null) > 0;
	}

	private async Task<ICollection<ObjectId>?> FindOutdated(DateTime cutoff)
	{
		var filter = JobFilter.And(
			JobFilter.Gt(d => d.Status, JobStatus.Ready),
			JobFilter.Gte(d => d.InvisibleUntil, DateTime.MaxValue.ToUtc()),
			JobFilter.Lt(d => d.ScheduledFor, cutoff.ToUtc()));

		return await (await Jobs)
			.Find(filter, FindObsoleteOptions)
			.Limit(PruneLimit)
			.Project(DocumentIdProjection)
			.ToListAsync();
	}
}