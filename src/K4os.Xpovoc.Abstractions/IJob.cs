using System;

namespace K4os.Xpovoc.Abstractions
{
	public interface IJob
	{
		Guid JobId { get; }
		DateTime UtcTime { get; }
		object Payload { get; }
		object Context { get; }
	}
}
