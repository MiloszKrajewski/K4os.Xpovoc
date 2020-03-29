using System;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;

namespace K4os.Xpovoc.MySql.Playground
{
	internal class NullJobHandler: IJobHandler
	{
		public Task Handle(CancellationToken token, object payload) => Task.CompletedTask;
	}
}
