using System;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;

namespace K4os.Xpovoc.Core.Queue;

public interface IJobQueueAdapter: IDisposable
{
	Task Publish(TimeSpan delay, IJob job, CancellationToken token);
	IDisposable Subscribe(Func<CancellationToken, IJob, Task> handler);
}
