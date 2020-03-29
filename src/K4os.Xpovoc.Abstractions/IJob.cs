using System;

namespace K4os.Xpovoc.Abstractions
{
	public interface IJob
	{
		Guid Id { get; }
		object Payload { get; }
		int Attempt { get; }
	}
}
