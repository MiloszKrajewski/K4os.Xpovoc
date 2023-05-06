using System;

namespace K4os.Xpovoc.Sqs.Internal;

internal static class SqsConstants
{
	public const int MaximumReceiveCount = 10;
	public const int MaximumNumberOfMessages = 10;
	public static readonly TimeSpan MaximumRetentionPeriod = TimeSpan.FromDays(14);
	public static readonly TimeSpan DefaultVisibilityTimeout = TimeSpan.FromSeconds(30);
	public static readonly TimeSpan DefaultReceiveMessageWait = TimeSpan.FromSeconds(20);
	public static readonly TimeSpan MaximumDelay = TimeSpan.FromMinutes(15);
}
