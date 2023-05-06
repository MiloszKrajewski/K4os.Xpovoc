using System;

namespace K4os.Xpovoc.Sqs.Internal;

public class SqsQueueSettings: ISqsQueueSettings
{
	public int? ReceiveCount { get; set; }
	public TimeSpan? RetentionPeriod { get; set; }
	public TimeSpan? VisibilityTimeout { get; set; }
	public TimeSpan? ReceiveMessageWait { get; set; }
}