using System;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.Core.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace K4os.Xpovoc.Test
{
	public class JobHandlerTests
	{
		[Fact]
		public async Task SimpleJobHandlerResolvesRightHandler()
		{
			var services = new ServiceCollection();
			var result = new TaskCompletionSource<string>();

			services.AddSingleton(result);
			services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
			services.AddSingleton<IJobScheduler, DbJobScheduler>();
			services.AddSingleton<IDbJobStorage, MemoryJobStorage>();
			services.AddSingleton<IJobHandler, SimpleJobHandler>();
			services.AddSingleton<IJobHandler<string>, StringMessageHandler>();

			var provider = services.BuildServiceProvider();

			var scheduler = provider.GetRequiredService<IJobScheduler>();
			var guid = Guid.NewGuid().ToString();
			await scheduler.Schedule(DateTimeOffset.Now, guid);

			Assert.True(result.Task.Wait(5000));
			Assert.Equal(guid, result.Task.Result);
			
			scheduler.Dispose();
		}

		public class StringMessageHandler: IJobHandler<string>
		{
			private readonly TaskCompletionSource<string> _result;

			public StringMessageHandler(TaskCompletionSource<string> result) => 
				_result = result;

			public Task Handle(CancellationToken token, string payload)
			{
				_result.SetResult(payload);
				return Task.CompletedTask;
			}
		}
	}
}
