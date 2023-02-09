using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using K4os.Xpovoc.Core.Db;
using Xunit;

namespace K4os.Xpovoc.Db.Test.Integrations;

public abstract class StorageTestBase
{
	private readonly XDocument _secrets = 
		Secrets.Load("databases.xml").Required();

	protected abstract IDbJobStorage CreateStorage(string schema);

	protected abstract void ClearStorage(string schema);

	protected abstract int CountJobs(string schema);

	protected string Secret(string path) => 
		(_secrets.XPathSelectElement(path)?.Value).Required();

	protected static DateTime Now => DateTime.UtcNow;

	[Theory, InlineData(""), InlineData("xpovoc")]
	public async Task JobCanBeAdded(string schema)
	{
		var payload = Guid.NewGuid();
		ClearStorage(schema);
		var storage = CreateStorage(schema);
		await storage.Schedule(payload, Now);
	}

	[Theory, InlineData(""), InlineData("xpovoc")]
	public async Task AddedJobCanBeClaimed(string schema)
	{
		var payload = Guid.NewGuid();
		var worker = Guid.NewGuid();
		ClearStorage(schema);
		var storage = CreateStorage(schema);
		await storage.Schedule(payload, Now.AddSeconds(-1));
		var job = await storage.Claim(
			CancellationToken.None, worker,
			Now, Now.AddSeconds(5));

		Assert.NotNull(job);
		Assert.Equal(payload, job.Payload);
	}

	[Theory, InlineData(""), InlineData("xpovoc")]
	public async Task ClaimedJobCanBeKept(string schema)
	{
		var payload = Guid.NewGuid();
		var worker = Guid.NewGuid();
		ClearStorage(schema);
		var storage = CreateStorage(schema);
		await storage.Schedule(payload, Now.AddSeconds(-1));
		var job = await storage.Claim(
			CancellationToken.None, worker,
			Now, Now.AddSeconds(5));

		Assert.NotNull(job);
		Assert.Equal(payload, job.Payload);

		var kept = await storage.KeepClaim(
			CancellationToken.None,
			worker, job, Now.AddSeconds(5));

		Assert.True(kept);
	}

	[Theory, InlineData(""), InlineData("xpovoc")]
	public async Task ClaimedJobCannotBeKeptBySomeoneElse(string schema)
	{
		var payload = Guid.NewGuid();
		var worker = Guid.NewGuid();
		ClearStorage(schema);
		var storage = CreateStorage(schema);
		await storage.Schedule(payload, Now.AddSeconds(-1));
		var job = await storage.Claim(
			CancellationToken.None, worker,
			Now, Now.AddSeconds(5));

		Assert.NotNull(job);
		Assert.Equal(payload, job.Payload);

		var otherWorker = Guid.NewGuid();
		var kept = await storage.KeepClaim(
			CancellationToken.None,
			otherWorker, job, Now.AddSeconds(5));

		Assert.False(kept);
	}

	[Theory, InlineData(""), InlineData("xpovoc")]
	public async Task JobsInFutureAreNotClaimed(string schema)
	{
		var payload = Guid.NewGuid();
		var worker = Guid.NewGuid();
		ClearStorage(schema);
		var storage = CreateStorage(schema);
		await storage.Schedule(payload, Now.AddDays(1));
		var job = await storage.Claim(
			CancellationToken.None, worker,
			Now, Now.AddSeconds(5));

		Assert.Null(job);
	}

	[Theory, InlineData(""), InlineData("xpovoc")]
	public async Task CompletedJobGetsDeleted(string schema)
	{
		var payload = Guid.NewGuid();
		var worker = Guid.NewGuid();
		ClearStorage(schema);
		var storage = CreateStorage(schema);
		await storage.Schedule(payload, Now.AddSeconds(-1));
		var job = await storage.Claim(
			CancellationToken.None, worker,
			Now, Now.AddSeconds(5));

		Assert.NotNull(job);
		Assert.Equal(payload, job.Payload);

		await storage.Complete(worker, job, Now);

		var anotherJob = await storage.Claim(
			CancellationToken.None, worker,
			Now, Now.AddSeconds(5));

		Assert.Null(anotherJob);
	}

	[Theory, InlineData(""), InlineData("xpovoc")]
	public async Task FailedJobCanBeRetried(string schema)
	{
		var payload = Guid.NewGuid();
		var worker = Guid.NewGuid();
		ClearStorage(schema);
		var storage = CreateStorage(schema);
		await storage.Schedule(payload, Now.AddSeconds(-1));
		var job = await storage.Claim(
			CancellationToken.None, worker,
			Now, Now.AddSeconds(5));

		Assert.NotNull(job);
		Assert.Equal(payload, job.Payload);
		Assert.Equal(1, job.Attempt);

		await storage.Retry(worker, job, Now.AddSeconds(-1));

		var anotherJob = await storage.Claim(
			CancellationToken.None, worker,
			Now, Now.AddSeconds(5));

		Assert.NotNull(anotherJob);
		Assert.Equal(payload, anotherJob.Payload);
		Assert.Equal(2, anotherJob.Attempt);
	}

	[Theory, InlineData(""), InlineData("xpovoc")]
	public async Task ForgottenJobsAreDeleted(string schema)
	{
		var payload = Guid.NewGuid();
		var worker = Guid.NewGuid();
		ClearStorage(schema);
		var storage = CreateStorage(schema);
		await storage.Schedule(payload, Now.AddSeconds(-1));
		var job = await storage.Claim(
			CancellationToken.None, worker,
			Now, Now.AddSeconds(5));

		Assert.NotNull(job);
		Assert.Equal(payload, job.Payload);

		await storage.Forget(worker, job, Now);

		var anotherJob = await storage.Claim(
			CancellationToken.None, worker,
			Now, Now.AddSeconds(5));

		Assert.Null(anotherJob);
	}

	[Theory, InlineData(""), InlineData("xpovoc")]
	public async Task PruneDeletesCompletedRowsFromDatabase(string schema)
	{
		var payload = Guid.NewGuid();
		var worker = Guid.NewGuid();
		ClearStorage(schema);
		var storage = CreateStorage(schema);
		await storage.Schedule(payload, Now.AddSeconds(-1));
		var job = await storage.Claim(
			CancellationToken.None, worker,
			Now, Now.AddSeconds(5));

		Assert.NotNull(job);
		Assert.Equal(payload, job.Payload);

		await storage.Complete(worker, job, Now);

		await storage.Prune(Now);

		Assert.Equal(0, CountJobs(schema));
	}

	[Theory, InlineData(""), InlineData("xpovoc")]
	public async Task PruneDeletesForgottenRowsFromDatabase(string schema)
	{
		var payload = Guid.NewGuid();
		var worker = Guid.NewGuid();
		ClearStorage(schema);
		var storage = CreateStorage(schema);
		await storage.Schedule(payload, Now.AddSeconds(-1));
		var job = await storage.Claim(
			CancellationToken.None, worker,
			Now, Now.AddSeconds(5));

		Assert.NotNull(job);
		Assert.Equal(payload, job.Payload);

		await storage.Forget(worker, job, Now);

		await storage.Prune(Now);

		Assert.Equal(0, CountJobs(schema));
	}
		
	[Theory, InlineData(""), InlineData("xpovoc")]
	public async Task PruneDoesNotDeletePendingJobs(string schema)
	{
		var payload = Guid.NewGuid();
		var worker = Guid.NewGuid();
		ClearStorage(schema);
		var storage = CreateStorage(schema);
		await storage.Schedule(payload, Now.AddDays(1));

		await storage.Prune(Now);

		Assert.Equal(1, CountJobs(schema));
	}

	public async Task MassPruningDoesNotThrowExceptionsImpl()
	{
		var schema = string.Empty;
		var payload = Guid.NewGuid();
		var worker = Guid.NewGuid();
		ClearStorage(schema);
		var storage = CreateStorage(schema);

		await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ScheduleLoop(2500)));
		await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => CompleteLoop()));
		await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => PruningLoop()));

		Assert.Equal(0, CountJobs(schema));

		async Task ScheduleLoop(int count)
		{
			while (count-- > 0) await storage.Schedule(payload, Now.AddSeconds(-1));
		}

		async Task CompleteLoop()
		{
			while (true)
			{
				var job = await storage.Claim(
					CancellationToken.None, worker,
					Now, Now.AddSeconds(5));
				if (job is null) break;

				await storage.Complete(worker, job, Now);
			}
		}

		async Task PruningLoop(bool more = true)
		{
			while (more) more = await storage.Prune(Now);
		}
	}
}