using K4os.Xpovoc.Abstractions;

namespace K4os.Xpovoc.Sqs.Test;

public class TestJob: IJob
{
	public Guid JobId { get; }
	public DateTime UtcTime { get; }
	public object? Payload { get; }
	public object? Context { get; }

	public TestJob(DateTime time, object payload)
	{
		JobId = Guid.NewGuid();
		UtcTime = time;
		Payload = payload;
		Context = null;
	}
}
