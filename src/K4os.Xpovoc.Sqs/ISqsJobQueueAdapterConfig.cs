using K4os.Xpovoc.Sqs.Internal;

namespace K4os.Xpovoc.Sqs;

public interface ISqsJobQueueAdapterConfig
{
	public string QueueName { get; }
	int Concurrency { get; set; }
	SqsQueueSettings? SqsQueueSettings { get; }
}

public class SqsJobQueueAdapterConfig: ISqsJobQueueAdapterConfig
{
	public string QueueName { get; set; } = null!;
	public int Concurrency { get; set; }
	SqsQueueSettings? ISqsJobQueueAdapterConfig.SqsQueueSettings => default;
}
