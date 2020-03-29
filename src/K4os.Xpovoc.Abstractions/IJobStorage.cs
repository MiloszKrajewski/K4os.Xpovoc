using System;
using System.Threading;
using System.Threading.Tasks;

namespace K4os.Xpovoc.Abstractions
{
	public interface IJobStorage
	{
		/// <summary>
		/// Claims the job for given worker.
		/// It needs to be guaranteed that no two worker can claim same job.
		/// Use distributed lock, database locks, unique indexes whatever works,
		/// but whatever happens do not allow two workers to claim same job.
		/// </summary>
		/// <param name="token">Cancellation token.</param>
		/// <param name="worker">Worker id.</param>
		/// <param name="now">Current time.</param>
		/// <param name="until">For how long it should be claimed for (at least).</param>
		/// <returns>Claimed job definition, or <c>null</c> if no job claimed.</returns>
		Task<IJob> Claim(
			CancellationToken token,
			Guid worker, DateTimeOffset now, DateTimeOffset until);

		Task<bool> KeepClaim(
			CancellationToken token, 
			Guid worker, Guid job, DateTimeOffset until);

		Task Complete(Guid worker, Guid job, DateTimeOffset now);
		Task Retry(Guid worker, Guid job, DateTimeOffset when);
		
		Task<Guid> Schedule(object payload, DateTimeOffset when);
		Task Reschedule(Guid job, DateTimeOffset when);
		Task Cancel(Guid job);
	}
}
