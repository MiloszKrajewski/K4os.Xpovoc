using System;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.Redis;
using StackExchange.Redis;

namespace K4os.Xpovoc.Db.Test.Integrations;

public class RedisStorageTest: StorageTestBase
{
	private string ConnectionString => Secret("/secrets/redis");

	protected override IDbJobStorage CreateStorage(string schema) =>
		new RedisJobStorage(
			ConnectionMultiplexer.Connect(ConnectionString).GetDatabase(),
			new RedisJobStorageConfig { Prefix = schema });

	protected override void ClearStorage(string schema)
	{
		var prefix = schema.NotBlank("xpovoc");
		using var connection = ConnectionMultiplexer.Connect(ConnectionString);
		var db = connection.GetDatabase();
		while (true)
		{
			var item = db.SortedSetPop($"{prefix}:queue");
			if (!item.HasValue) break;

			db.KeyDelete($"{prefix}:job:{item.Value.Element}");
		}
	}

	protected override int CountJobs(string schema)
	{
		var prefix = schema.NotBlank("xpovoc");
		using var connection = ConnectionMultiplexer.Connect(ConnectionString);
		return (int)connection.GetDatabase().SortedSetLength($"{prefix}:queue");
	}
}