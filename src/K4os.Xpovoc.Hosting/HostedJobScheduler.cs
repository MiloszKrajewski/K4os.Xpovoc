using System;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace K4os.Xpovoc.Hosting
{
	public class HostedJobScheduler: IHostedService
	{
		private readonly IServiceProvider _serviceProvider;
		private IJobScheduler? _scheduler;

		public HostedJobScheduler(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			_scheduler = _serviceProvider.GetRequiredService<IJobScheduler>();
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			_scheduler?.Dispose();
			_scheduler = null;
			return Task.CompletedTask;
		}
	}
}
