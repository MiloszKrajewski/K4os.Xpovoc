using System;
using System.Threading;
using System.Threading.Tasks;
using K4os.Quarterback;
using K4os.Xpovoc.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace K4os.Xpovoc.Quarterback
{
	public class QuarterbackJobHandler: IJobHandler
	{
		private readonly IServiceProvider _provider;

		public QuarterbackJobHandler(IServiceProvider provider) =>
			_provider = provider ?? throw new ArgumentNullException(nameof(provider));

		public async Task Handle(CancellationToken token, object payload)
		{
			using var scope = _provider.CreateScope();
			var provider = scope.ServiceProvider;

			await (IsEvent(payload) switch {
				true => provider.PublishAny(payload, token),
				_ => provider.SendAny(payload, token),
			});
		}

		protected virtual bool IsEvent(object payload) => false;
	}
}
