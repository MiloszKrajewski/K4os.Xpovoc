using System;

namespace K4os.Xpovoc.Core.Db
{
	public interface ISchedulerConfig
	{
		int WorkerCount { get; }
		TimeSpan PollInterval { get; }
		TimeSpan KeepAliveInterval { get; }
		TimeSpan KeepAlivePeriod { get; }
		TimeSpan KeepAliveRetryInterval { get; }
		TimeSpan RetryInterval { get; }
		double RetryFactor { get; }
		int RetryLimit { get; }
		TimeSpan MaximumRetryInterval { get; }
		TimeSpan KeepFinishedJobsPeriod { get; }
		TimeSpan PruneInterval { get; set; }
	}
}
