using System;
using System.Threading.Tasks;

namespace K4os.Xpovoc.Abstractions
{
	public interface IJobScheduler: IDisposable, IDateTimeSource
	{
		Task<Guid> Schedule(object payload, DateTimeOffset time);
		Task Reschedule(Guid job, DateTimeOffset when);
		Task Cancel(Guid job);
	}
}
