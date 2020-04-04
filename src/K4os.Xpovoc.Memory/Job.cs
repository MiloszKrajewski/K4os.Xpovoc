using System;
using System.Linq;
using K4os.Xpovoc.Abstractions;

namespace K4os.Xpovoc.Memory
{
	internal class Job: IJob
	{
		public Guid Id { get; set; }
		public object Payload { get; set; }
		public int Attempt { get; set; }
		public DateTime ScheduledFor { get; set; }
		public DateTime InvisibleUntil { get; set; }
	}
}
