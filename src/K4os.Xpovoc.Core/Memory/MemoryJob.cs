using System;
using K4os.Xpovoc.Core.Db;

namespace K4os.Xpovoc.Core.Memory;

internal class MemoryJob: IDbJob
{
	public Guid JobId { get; set; }
	public DateTime UtcTime => ScheduledFor;
	public object? Payload { get; set; }
	public object? Context { get; set; }
	public int Attempt { get; set; }
	public DateTime ScheduledFor { get; set; }
	public DateTime InvisibleUntil { get; set; }
	public Guid? Claimed { get; set; }
}