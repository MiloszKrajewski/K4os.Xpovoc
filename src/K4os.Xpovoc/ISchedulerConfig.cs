using System;

namespace K4os.Xpovoc
{
	public interface ISchedulerConfig
	{
		int WorkerCount { get; set; }
		TimeSpan PollInterval { get; set; }
		TimeSpan KeepAliveInterval { get; set; }
		TimeSpan KeepAlivePeriod { get; set; }
		TimeSpan KeepAliveRetryInterval { get; set; }
		TimeSpan RetryInterval { get; set; }
		double RetryFactor { get; set; }
		int RetryLimit { get; set; }
		TimeSpan MaximumRetryInterval { get; set; }
	}
}
