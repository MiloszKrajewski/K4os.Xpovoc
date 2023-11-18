using System;
using K4os.Xpovoc.Sqs.Internal;

namespace K4os.Xpovoc.Sqs;

public interface ISqsJobQueueAdapterSettings
{
	public string QueueName { get; }
	int PushConcurrency { get; set; }
	int PullConcurrency { get; set; }
	int JobConcurrency { get; set; }
	TimeSpan RetryInterval { get; set; }
	int RetryCount { get; set; }
	ISqsQueueSettings? QueueSettings { get; set; }
}

public class SqsJobQueueAdapterSettings: ISqsJobQueueAdapterSettings
{
	public string QueueName { get; set; } = "xpovoc-scheduler";
	public int PushConcurrency { get; set; } = 4;
	public int PullConcurrency { get; set; } = 1;
	public int JobConcurrency { get; set; } = 4;
	public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(1);
	public int RetryCount { get; set; } = 0;
	public ISqsQueueSettings? QueueSettings { get; set; } = new SqsQueueSettings();
}
