using K4os.Xpovoc.Sqs.Internal;

namespace K4os.Xpovoc.Sqs.Test;

public class TestConfig: ISqsJobQueueAdapterConfig
{
	public string QueueName { get; set; } = null!;
	public int Concurrency { get; set; }
	public SqsQueueSettings? SqsQueueSettings { get; set; }
}
