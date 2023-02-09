using System;
using K4os.Xpovoc.Core.Db;

namespace K4os.Xpovoc.Redis;

internal class RedisDbJob: IDbJob
{
	public RedisDbJob(Guid jobId) { JobId = jobId; }

	public Guid JobId { set; get; }
	public DateTime UtcTime { set; get; }
	public object? Payload { set; get; }
	public object? Context { set; get; }
	public int Attempt { set; get; }
}
