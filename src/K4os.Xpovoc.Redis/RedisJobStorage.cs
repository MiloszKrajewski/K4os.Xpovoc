using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.Core.Sql;
using K4os.Xpovoc.Redis.Resources;
using StackExchange.Redis;

namespace K4os.Xpovoc.Redis;

public class RedisJobStorage: IDbJobStorage
{
	private static readonly Task<bool> AlwaysFalse = Task.FromResult(false);

	private readonly IDatabase _storage;
	private readonly IJobSerializer _serializer;
	private readonly LuaScript _claimScript;
	private readonly string _prefix;

	public RedisJobStorage(
		IDatabase storage,
		IRedisJobStorageConfig config,
		IJobSerializer? serializer = null)
	{
		_storage = storage;
		_prefix = config.Prefix.NotBlank("xpovoc")!;
		_serializer = serializer ?? new DefaultJobSerializer();

		var functions = RedisResourceLoader.Default.LoadFunctions();
		_claimScript = LuaScript.Prepare(functions["claim"]);
	}
	
	private RedisKey JobKey(string hashId) => $"{_prefix}:job:{hashId}";
	private RedisKey JobKey(Guid jobId) => JobKey(JobRef(jobId));
	private static RedisValue JobRef(Guid jobId) => jobId.ToString("N");
	private RedisKey QueueKey => $"{_prefix}:queue";
	private static double ScoreOf(DateTime now) => now.ToOADate();

	private RedisValue Serialize(object payload) => _serializer.Serialize(payload);
	private object Deserialize(RedisValue payloadX) => _serializer.Deserialize(payloadX);

	public async Task<Guid> Schedule(object payload, DateTime when)
	{
		var jobId = Guid.NewGuid();
		
		var transaction = _storage.CreateTransaction();
		_ = transaction.HashSetAsync(JobKey(jobId), CreateHash(jobId, payload, when));
		_ = transaction.SortedSetAddAsync(QueueKey, JobRef(jobId), ScoreOf(when));
		await transaction.ExecuteAsync();

		return jobId;
	}

	private HashEntry[] CreateHash(Guid jobId, object payload, DateTime scheduledFor) =>
		new[] {
			new HashEntry("job_id", jobId.ToString("D")),
			new HashEntry("payload", Serialize(payload)),
			new HashEntry("scheduled_for", scheduledFor.ToUtc().ToString("O")),
			new HashEntry("attempt", 0),
		};

	private IDbJob? ParseHash(string hashId, HashEntry[]? hash)
	{
		if (hash is null) return null;

		var map = hash.ToDictionary();

		var jobId = Guid.Parse(map["job_id"]);
		var payload = Deserialize(map["payload"]);
		var scheduledFor = DateTime.Parse(map["scheduled_for"]).ToUtc();
		var attempt = (int)map["attempt"];

		return new RedisDbJob(jobId) {
			Context = hashId,
			Payload = payload,
			UtcTime = scheduledFor,
			Attempt = attempt,
		};
	}

	public async Task<IDbJob?> Claim(
		CancellationToken token, Guid worker, DateTime now, DateTime until)
	{
		var result = await _storage.ScriptEvaluateAsync(
			_claimScript, new {
				queue = QueueKey,
				now = ScoreOf(now),
				until = ScoreOf(until),
			});

		var hashId = result.Type switch {
			ResultType.SimpleString or ResultType.BulkString => (string)result,
			ResultType.None => null,
			_ => throw new IOException($"Unexpected result type: {result.Type}"),
		};
		if (hashId is null) return null;
		
		var transaction = _storage.CreateTransaction();
		_ = transaction.HashIncrementAsync(JobKey(hashId), "attempt");
		_ = transaction.HashSetAsync(JobKey(hashId), "claimed_by", worker.ToString("D"));
		await transaction.ExecuteAsync();
		
		return await CreateDbJob(hashId);
	}

	private async Task<IDbJob?> CreateDbJob(string hashId)
	{
		var hashData = await _storage.HashGetAllAsync(JobKey(hashId));
		return ParseHash(hashId, hashData);
	}

	public async Task<bool> KeepClaim(
		CancellationToken token, Guid worker, IDbJob job, DateTime until)
	{
		var jobId = job.JobId;
		Guid.TryParse(await _storage.HashGetAsync(JobKey(jobId), "claimed_by"), out var claimedBy);
		if (claimedBy != worker) return false;

		await _storage.SortedSetAddAsync(QueueKey, JobRef(jobId), ScoreOf(until));
		return true;
	}

	private async Task DeleteJob(IJob job)
	{
		var jobId = job.JobId;
		var transaction = _storage.CreateTransaction();
		_ = transaction.SortedSetRemoveAsync(QueueKey, JobRef(jobId));
		_ = transaction.KeyDeleteAsync(JobKey(jobId));
		await transaction.ExecuteAsync();
	}

	public Task Complete(Guid worker, IDbJob job, DateTime now) => DeleteJob(job);

	public Task Forget(Guid worker, IDbJob job, DateTime now) => DeleteJob(job);

	public async Task Retry(Guid worker, IDbJob job, DateTime when)
	{
		var jobId = job.JobId;
		var transaction = _storage.CreateTransaction();
		_ = transaction.SortedSetAddAsync(QueueKey, JobRef(jobId), ScoreOf(when));
		_ = transaction.HashDeleteAsync(JobKey(jobId), "claimed_by");
		await transaction.ExecuteAsync();
	}

	public Task<bool> Prune(DateTime cutoff) => AlwaysFalse;
}
