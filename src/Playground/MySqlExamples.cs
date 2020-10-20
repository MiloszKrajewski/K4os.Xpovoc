using System;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.Core.Sql;
using K4os.Xpovoc.MySql;
using Microsoft.Extensions.DependencyInjection;

namespace Playground
{
	internal class MySqlExamples
	{
		public static void Configure(IServiceCollection collection)
		{
			collection.AddSingleton<IJobHandler>(new NullJobHandler());

			collection.AddSingleton<IJobScheduler, DbJobScheduler>();
			collection.AddSingleton<ISchedulerConfig, SchedulerConfig>();
			collection.AddSingleton(SystemDateTimeSource.Default);
			collection.AddSingleton<IDbJobStorage, MySqlJobStorage>();
			collection.AddSingleton<IJobSerializer, DefaultJobSerializer>();
			collection.AddSingleton<IMySqlJobStorageConfig>(
				new MySqlJobStorageConfig { ConnectionString = "..." });
		}

		public static void Startup(IServiceProvider provider)
		{
			_ = provider.GetService<IJobScheduler>();
		}
	}
}
