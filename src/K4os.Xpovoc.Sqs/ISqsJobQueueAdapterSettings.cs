using System;
using K4os.Xpovoc.Sqs.Internal;

namespace K4os.Xpovoc.Sqs;

public interface ISqsJobQueueAdapterConfig
{
	public string QueueName { get; }
	int? PushConcurrency { get; set; }
	int? PullConcurrency { get; set; }
	int? ExecConcurrency { get; set; }
	TimeSpan? RetryInterval { get; set; }
	int? RetryCount { get; set; }
	ISqsQueueSettings? QueueSettings { get; set; }
}

public class SqsJobQueueAdapterConfig: ISqsJobQueueAdapterConfig
{
	public string QueueName { get; set; } = null!;
	public int? PushConcurrency { get; set; }
	public int? PullConcurrency { get; set; }
	public int? ExecConcurrency { get; set; }
	public TimeSpan? RetryInterval { get; set; } 
	public int? RetryCount { get; set; }
	public ISqsQueueSettings? QueueSettings { get; set; }
}
