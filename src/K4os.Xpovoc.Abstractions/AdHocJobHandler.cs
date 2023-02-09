using System;
using System.Threading;
using System.Threading.Tasks;

namespace K4os.Xpovoc.Abstractions;

public class AdHocJobHandler: IJobHandler
{
	private readonly Func<object, Task> _handler;

	public AdHocJobHandler(Func<object, Task> handler) =>
		_handler = handler ?? throw new ArgumentNullException(nameof(handler));

	public AdHocJobHandler(Action<object> handler) =>
		_handler = Wrap(handler ?? throw new ArgumentNullException(nameof(handler)));

	private static Func<object, Task> Wrap(Action<object> handler) =>
		o => {
			handler(o);
			return Task.CompletedTask;
		};

	public Task Handle(CancellationToken token, object payload) =>
		_handler(payload);
}