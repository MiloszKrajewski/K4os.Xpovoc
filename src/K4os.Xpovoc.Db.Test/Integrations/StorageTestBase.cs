using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using K4os.Xpovoc.Core.Db;
using Xunit;

namespace K4os.Xpovoc.Db.Test.Integrations
{
	public abstract class StorageTestBase
	{
		private readonly XDocument _secrets;

		protected StorageTestBase() { _secrets = Secrets.Load(".secrets.xml"); }

		protected abstract IDbJobStorage CreateStorage(string schema);

		protected abstract void ClearStorage(string schema);

		protected string Secret(string path) => _secrets.XPathSelectElement(path)?.Value;

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
		public async Task ClaimedJobCannotBeKeptBeSomeoneElse(string schema)
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
	}
}
