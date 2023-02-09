using System;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;

namespace Playground;

internal class NullJobHandler: IJobHandler
{
	public Task Handle(CancellationToken token, object payload) => Task.CompletedTask;
}