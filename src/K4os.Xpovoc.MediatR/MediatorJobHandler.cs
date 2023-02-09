using System;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace K4os.Xpovoc.MediatR;

public class MediatorJobHandler: IJobHandler
{
	private readonly IServiceProvider _provider;

	public MediatorJobHandler(IServiceProvider provider)
	{
		_provider = provider ?? throw new ArgumentNullException(nameof(provider));
	}

	public Task Handle(CancellationToken token, object payload) =>
		payload switch {
			INotification => InNewScope(m => m.Publish(payload, token)),
			IRequest => InNewScope(m => m.Send(payload, token)),
			_ => throw InvalidMessageType(payload)
		};

	private async Task InNewScope(Func<IMediator, Task> action)
	{
		using var scope = _provider.CreateScope();
		var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
		await action(mediator);
	}

	private static ArgumentException InvalidMessageType(object payload) =>
		new(
			$"{payload.GetType().Name} is neither {nameof(IRequest)} " +
			$"nor {nameof(INotification)} so it cannot by handled by MediatR"
		);
}