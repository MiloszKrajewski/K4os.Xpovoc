using System;
using System.Threading;
using System.Threading.Tasks;

namespace K4os.Xpovoc.Core.Db;

public interface IDbJobStorage
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
	Task<IDbJob?> Claim(
		CancellationToken token,
		Guid worker, DateTime now, DateTime until);

	Task<bool> KeepClaim(
		CancellationToken token, 
		Guid worker, IDbJob job, DateTime until);

	Task Complete(Guid worker, IDbJob job, DateTime now);
	Task Forget(Guid worker, IDbJob job, DateTime now);
	Task Retry(Guid worker, IDbJob job, DateTime when);
		
	Task<Guid> Schedule(object payload, DateTime when);
		
	/// <summary>
	/// Performs pruning of old jobs (if they are persisted).
	/// This method is called in intervals by scheduler itself.
	/// Returns <c>true</c> if there are more things to do or <c>false</c> when
	/// database is in pretty clean state.
	/// Please note that this method is called from all instances so it may require
	/// some locking on some databases.
	/// </summary>
	/// <param name="cutoff">Date in the past defining the cut-off point
	/// (finished jobs older than that can be deleted)</param>
	/// <returns><c>true</c> if more work is needed, <c>false</c> if database is
	/// clean at the moment.</returns>
	Task<bool> Prune(DateTime cutoff);
}