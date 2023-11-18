using System;
using System.Threading.Tasks;

namespace K4os.Xpovoc.Abstractions;

public interface IJobScheduler: IDisposable
{
	Task<Guid> Schedule(DateTimeOffset time, object payload);
}