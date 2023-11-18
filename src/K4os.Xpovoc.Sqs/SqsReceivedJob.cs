using System;
using Amazon.SQS.Model;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Sqs.Internal;

namespace K4os.Xpovoc.Sqs;

internal class SqsReceivedJob: IJob
{
	private readonly Message _message;
	private readonly IJobSerializer _serializer;

	private Guid? _jobId;
	private DateTime? _scheduledFor;
	private object? _payload;

	public SqsReceivedJob(Message message, IJobSerializer serializer)
	{
		_message = message;
		_serializer = serializer;
	}

	public Guid JobId =>
		_jobId ??= _message.TryGetStringAttribute(SqsAttributes.JobId) switch {
			{ } attr when Guid.TryParse(attr, out var parsed) => parsed,
			_ => throw new ArgumentException($"Invalid '{SqsAttributes.JobId}' attribute"),
		};

	public DateTime UtcTime =>
		_scheduledFor ??= _message.TryGetStringAttribute(SqsAttributes.ScheduledFor) switch {
			{ } attr when DateTime.TryParse(attr, out var parsed) => parsed,
			_ => throw new ArgumentException($"Invalid '{SqsAttributes.ScheduledFor}' attribute"),
		};

	public object Payload =>
		_payload ??= _serializer.Deserialize(_message.Body);

	public object Context => _message;
}
