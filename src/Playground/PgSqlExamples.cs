using System;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.Json;
using K4os.Xpovoc.PgSql;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Playground
{
	internal class PgSqlExamples
	{
		public static void Configure(IServiceCollection collection)
		{
			collection.AddSingleton<IJobHandler, SimpleJobHandler>();

			collection.AddSingleton<IJobScheduler, DbJobScheduler>();
			collection.AddSingleton<IDbJobStorage, PgSqlJobStorage>();
			collection.AddSingleton<IPgSqlJobStorageConfig>(
				new PgSqlJobStorageConfig { ConnectionString = "..." });

			collection.AddSingleton<IJobSerializer>(
				new JsonJobSerializer(
					new JsonSerializerSettings {
						TypeNameHandling = TypeNameHandling.Auto
					}));
		}

		public static void Startup(IServiceProvider provider)
		{
			_ = provider.GetService<IJobScheduler>();
		}
	}
}
