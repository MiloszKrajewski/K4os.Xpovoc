using System;

namespace K4os.Xpovoc.Abstractions
{
	public class Job: IJob
	{
		public Guid Id { get; }

		public object Payload { get; }

		public int Attempt { get; }

		public Job(Guid id, object payload, int attempt) =>
			(Id, Payload, Attempt) = (id, payload, attempt);
	}
}
