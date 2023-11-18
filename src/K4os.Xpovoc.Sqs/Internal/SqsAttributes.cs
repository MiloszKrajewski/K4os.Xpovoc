using System;
using System.Collections.Generic;
using Amazon.SQS.Model;

namespace K4os.Xpovoc.Sqs.Internal;

internal static class SqsAttributes
{
	public static readonly List<string> All = new() { "All" };
	public const string JobId = "Xpovoc.JobId";
	public const string ScheduledFor = "Xpovoc.ScheduledFor";

	public static string? TryGetStringAttribute(this Message message, string name) =>
		message.MessageAttributes.TryGetOrDefault(name)?.StringValue;
}
