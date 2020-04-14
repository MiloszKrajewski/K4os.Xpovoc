using System;

namespace K4os.Xpovoc.Toolbox.Db
{
	public class SchedulerConfig: ISchedulerConfig
	{
		public static readonly SchedulerConfig Default = new SchedulerConfig {
			WorkerCount = 4,
			PollInterval = TimeSpan.FromSeconds(15),
			KeepAliveInterval = TimeSpan.FromSeconds(5),
			KeepAlivePeriod = TimeSpan.FromMinutes(1),
			KeepAliveRetryInterval = TimeSpan.FromSeconds(1),
			RetryLimit = 10,
			RetryInterval = TimeSpan.FromSeconds(30),
			MaximumRetryInterval = TimeSpan.FromHours(6),
			RetryFactor = 1.5,
		};

		public TimeSpan PollInterval { get; set; }

		public TimeSpan KeepAliveInterval { get; set; }

		public TimeSpan KeepAlivePeriod { get; set; }

		public TimeSpan RetryInterval { get; set; }

		public double RetryFactor { get; set; }

		public int RetryLimit { get; set; }

		public TimeSpan MaximumRetryInterval { get; set; }

		public TimeSpan KeepAliveRetryInterval { get; set; }

		public int WorkerCount { get; set; }
	}
}
