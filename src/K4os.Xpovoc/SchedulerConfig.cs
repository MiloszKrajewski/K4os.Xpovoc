using System;

namespace K4os.Xpovoc
{
	public class SchedulerConfig: ISchedulerConfig
	{
		public static readonly SchedulerConfig Default = new SchedulerConfig {
			WorkerCount = 4,
			PollInterval = TimeSpan.FromSeconds(15),
			KeepAliveInterval = TimeSpan.FromSeconds(5),
			KeepAlivePeriod = TimeSpan.FromMinutes(1),
			KeepAliveRetryInterval = TimeSpan.FromSeconds(1),
			RetryInterval = TimeSpan.FromSeconds(30),
		};

		public TimeSpan PollInterval { get; set; }

		public TimeSpan KeepAliveInterval { get; set; }

		public TimeSpan KeepAlivePeriod { get; set; }

		public TimeSpan RetryInterval { get; set; }

		public TimeSpan KeepAliveRetryInterval { get; set; }

		public int WorkerCount { get; set; }
	}
}
