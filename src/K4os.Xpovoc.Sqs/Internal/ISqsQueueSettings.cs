using System;

namespace K4os.Xpovoc.Sqs.Internal;

public interface ISqsQueueSettings
{
	int? ReceiveCount { get; }
	TimeSpan? RetentionPeriod { get; }
	TimeSpan? VisibilityTimeout { get; }
	TimeSpan? ReceiveMessageWait { get; }
}
