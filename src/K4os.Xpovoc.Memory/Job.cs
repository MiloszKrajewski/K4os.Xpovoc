using System;
using System.Linq;
using K4os.Xpovoc.Core.Db;

namespace K4os.Xpovoc.Memory
{
	internal class Job: IDbJob
	{
		public Guid JobId { get; set; }
		public DateTime UtcTime => ScheduledFor;
		public object Payload { get; set; }
		public object Context => null;
		public int Attempt { get; set; }
		public DateTime ScheduledFor { get; set; }
		public DateTime InvisibleUntil { get; set; }
	}
}
