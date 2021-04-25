using System;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace K4os.Xpovoc.Core
{
	public abstract class NewScopeJobHandler: IJobHandler
	{
		private readonly IServiceProvider _provider;

		protected NewScopeJobHandler(IServiceProvider provider)
		{
			_provider = provider ?? throw new ArgumentNullException(nameof(provider));
		}

		async Task IJobHandler.Handle(CancellationToken token, object payload)
		{
			using var scope = _provider.CreateScope();
			await Handle(token, scope.ServiceProvider, payload);
		}

		protected abstract Task Handle(
			CancellationToken token, IServiceProvider services, object payload);
	}
}
