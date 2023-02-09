using System;
using System.Threading.Tasks;

namespace K4os.Xpovoc.Abstractions;

public interface IJobScheduler: IDisposable, IDateTimeSource
{
	Task<Guid> Schedule(DateTimeOffset time, object payload);
}