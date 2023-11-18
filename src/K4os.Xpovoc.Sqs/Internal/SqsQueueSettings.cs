using System;

namespace K4os.Xpovoc.Sqs.Internal;

public class SqsQueueSettings: ISqsQueueSettings
{
	public int ReceiveCount { get; set; } = SqsConstants.MaximumReceiveCount;
	public TimeSpan RetentionPeriod { get; set; } = SqsConstants.MaximumRetentionPeriod;
	public TimeSpan VisibilityTimeout { get; set; } = SqsConstants.DefaultVisibilityTimeout; 
	public TimeSpan ReceiveMessageWait { get; set; } = SqsConstants.DefaultReceiveMessageWait; 
}