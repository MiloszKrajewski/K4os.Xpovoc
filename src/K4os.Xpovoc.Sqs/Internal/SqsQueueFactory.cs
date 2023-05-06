using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using ThirdParty.Json.LitJson;

namespace K4os.Xpovoc.Sqs.Internal;

public class SqsQueueFactory: ISqsQueueFactory
{
	private readonly IAmazonSQS _client;

	public SqsQueueFactory(IAmazonSQS client) { _client = client; }

	public async Task<ISqsQueue> Create(string queueName, SqsQueueSettings settings)
	{
		var queueUrl =
			await FindOrCreateQueue(queueName, settings) ??
			throw new InvalidOperationException($"Failed to find queue: {queueName}");
		return new SqsQueue(_client, queueUrl);
	}

	private async Task<string> FindOrCreateQueue(string queueName, SqsQueueSettings settings)
	{
		if (await TryFindQueue(queueName) is { } queueUrl)
			return queueUrl;

		var deadLetterName = queueName + "-dlq";

		var deadLetterUrl =
			await TryCreateQueue(deadLetterName, null, new SqsQueueSettings()) ??
			throw new InvalidOperationException(
				$"Failed to create dead letter queue: {deadLetterName}");

		return
			await TryCreateQueue(queueName, deadLetterUrl, settings) ??
			throw new InvalidOperationException($"Failed to create queue: {queueName}");
	}

	private async Task<string?> TryCreateQueue(
		string queueName, string? deadLetterUrl, SqsQueueSettings settings)
	{
		var queueSettings = new Dictionary<string, string>();

		queueSettings.AddIfNotNull(
			QueueAttributeName.MessageRetentionPeriod,
			ToQueueAttribute(
				settings.RetentionPeriod ?? SqsConstants.MaximumRetentionPeriod));

		queueSettings.AddIfNotNull(
			QueueAttributeName.VisibilityTimeout,
			ToQueueAttribute(
				settings.VisibilityTimeout ?? SqsConstants.DefaultVisibilityTimeout));

		queueSettings.AddIfNotNull(
			QueueAttributeName.ReceiveMessageWaitTimeSeconds,
			ToQueueAttribute(
				settings.ReceiveMessageWait ?? SqsConstants.DefaultReceiveMessageWait));

		if (deadLetterUrl is not null)
		{
			var deadLetterArn = await GetQueueArn(deadLetterUrl);
			queueSettings.Add(
				QueueAttributeName.RedrivePolicy,
				ToRedrivePolicy(
					deadLetterArn,
					settings.ReceiveCount));
		}

		try
		{
			var response = await _client.CreateQueueAsync(
				new CreateQueueRequest {
					QueueName = queueName,
					Attributes = queueSettings,
				});

			return response.QueueUrl;
		}
		catch (QueueNameExistsException)
		{
			return await TryFindQueue(queueName);
		}
	}

	private async Task<string> GetQueueArn(string queueUrl)
	{
		var response = await _client.GetQueueAttributesAsync(
			new GetQueueAttributesRequest {
				QueueUrl = queueUrl,
				AttributeNames = SqsAttributes.All,
			});

		return response.QueueARN;
	}

	private async Task<string?> TryFindQueue(string queueName)
	{
		try
		{
			var response = await _client.GetQueueUrlAsync(
				new GetQueueUrlRequest { QueueName = queueName });
			return response.QueueUrl;
		}
		catch (QueueDoesNotExistException)
		{
			return null;
		}
	}

	private static string? ToQueueAttribute(TimeSpan? timespan) =>
		timespan is not null
			? ((int)timespan.Value.TotalSeconds.NotLessThan(0)).ToString()
			: null;

	private static string ToRedrivePolicy(string queueArn, int? maxReceiveCount)
	{
		var values = new Dictionary<string, object> {
			{ "deadLetterTargetArn", queueArn },
			{ "maxReceiveCount", maxReceiveCount ?? SqsConstants.MaximumReceiveCount },
		};

		return JsonMapper.ToJson(values);
	}
}