using System;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace K4os.Xpovoc.MediatR
{
	public class JobHandlerMediatorAdapter: IJobHandler
	{
		private readonly IServiceProvider _provider;

		public JobHandlerMediatorAdapter(IServiceProvider provider)
		{
			_provider = provider ?? throw new ArgumentNullException(nameof(provider));
		}

		public Task Handle(CancellationToken token, object payload)
		{
			switch (payload)
			{
				case INotification _: return InNewScope(m => m.Publish(payload, token));
				case IRequest _: return InNewScope(m => m.Send(payload, token));
				default: throw InvalidMessageType(payload);
			}
		}

		private async Task InNewScope(Func<IMediator, Task> action)
		{
			using (var scope = _provider.CreateScope())
				await action(scope.ServiceProvider.GetService<IMediator>());
		}

		private static ArgumentException InvalidMessageType(object payload) =>
			new ArgumentException(
				string.Format(
					"{0} is neither {1} nor {2} so it cannot by handled by MediatR",
					payload.GetType().Name,
					nameof(IRequest),
					nameof(INotification)));
	}
}
