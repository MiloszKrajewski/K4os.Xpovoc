using System;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;

namespace K4os.Xpovoc.MySql.Playground
{
	internal class AdHocJobHandler: IJobHandler
	{
		private readonly Action<object> _handler;

		public AdHocJobHandler(Action<object> handler) { _handler = handler; }

		public Task Handle(CancellationToken token, object payload)
		{
			_handler(payload);
			return Task.CompletedTask;
		}
	}
}
