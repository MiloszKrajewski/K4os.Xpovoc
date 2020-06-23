using System;

namespace K4os.Xpovoc.Core.Db
{
	public class SchedulerConfig: ISchedulerConfig
	{
		public static readonly SchedulerConfig Default = new SchedulerConfig();

		public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);

		public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(5);

		public TimeSpan KeepAlivePeriod { get; set; } = TimeSpan.FromMinutes(1);

		public TimeSpan KeepAliveRetryInterval { get; set; } = TimeSpan.FromSeconds(1);

		public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(30);

		public int RetryLimit { get; set; } = 10;

		public double RetryFactor { get; set; } = 1.5;

		public TimeSpan MaximumRetryInterval { get; set; } = TimeSpan.FromHours(6);

		public int WorkerCount { get; set; } = 4;
	}
}
