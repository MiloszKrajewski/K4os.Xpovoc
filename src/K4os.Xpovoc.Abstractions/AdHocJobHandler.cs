using System;
using System.Threading;
using System.Threading.Tasks;

namespace K4os.Xpovoc.Abstractions
{
	public class AdHocJobHandler: IJobHandler
	{
		private readonly Action<object> _handler;

		public AdHocJobHandler(Action<object> handler) =>
			_handler = handler ?? throw new ArgumentNullException(nameof(handler));

		public Task Handle(CancellationToken token, object payload)
		{
			_handler(payload);
			return Task.CompletedTask;
		}
	}
}
