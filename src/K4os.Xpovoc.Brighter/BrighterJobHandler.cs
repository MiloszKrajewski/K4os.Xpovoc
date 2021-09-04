using System;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using Paramore.Brighter;
using Microsoft.Extensions.DependencyInjection;

namespace K4os.Xpovoc.Brighter
{
	public class BrighterJobHandler: IJobHandler
	{
		private readonly IServiceProvider _provider;

		public BrighterJobHandler(IServiceProvider provider)
		{
			_provider = provider ?? throw new ArgumentNullException(nameof(provider));
		}

		public Task Handle(CancellationToken token, object payload)
		{
			if (payload is not IRequest)
				throw InvalidMessageType(payload);

			Task Process(IAmACommandProcessor processor) =>
				IsEvent(payload)
					? processor.PublishAsync((dynamic)payload, false, token)
					: processor.SendAsync((dynamic)payload, false, token);

			return InNewScope(Process);
		}

		protected virtual bool IsEvent(object payload) => false;

		private async Task InNewScope(Func<IAmACommandProcessor, Task> action)
		{
			using var scope = _provider.CreateScope();
			var processor = scope.ServiceProvider.GetService<IAmACommandProcessor>();
			await action(processor);
		}

		private static ArgumentException InvalidMessageType(object payload) =>
			new ArgumentException(
				string.Format(
					"{0} is not {1} so it cannot by handled by Brighter",
					payload.GetType().Name, nameof(IRequest)));
	}
}
