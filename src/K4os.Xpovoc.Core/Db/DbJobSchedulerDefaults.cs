using System;
using System.Linq;

namespace K4os.Xpovoc.Core.Db
{
	internal class DbJobSchedulerDefaults
	{
		public const double KeepAliveFactor = 3;
		public const int MaximumRetryFactor = 10;

		public static readonly TimeSpan MinimumPollInterval = TimeSpan.FromMilliseconds(100);
		public static readonly TimeSpan MinimumRetryInterval = TimeSpan.FromSeconds(1);
		public static readonly TimeSpan MinimumKeepAliveInterval = TimeSpan.FromSeconds(1);
		public static readonly TimeSpan MinimumKeepAlivePeriod = TimeSpan.FromMinutes(1);
		public static readonly TimeSpan MaximumRetryInterval = TimeSpan.FromDays(1);
	}
}
