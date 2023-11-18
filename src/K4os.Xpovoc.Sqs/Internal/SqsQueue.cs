using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace K4os.Xpovoc.Sqs.Internal;

internal class SqsQueue: ISqsQueue
{
	private readonly IAmazonSQS _client;
	private readonly string _queueUrl;
	private GetQueueAttributesResponse? _attributes;

	public string Url => _queueUrl;
	
	public SqsQueue(IAmazonSQS client, string queueUrl)
	{
		_client = client;
		_queueUrl = queueUrl;
	}

	public async Task<GetQueueAttributesResponse> GetAttributes(
		CancellationToken token = default) =>
		_attributes ??= await _client.GetQueueAttributesAsync(
			new GetQueueAttributesRequest {
				QueueUrl = _queueUrl,
				AttributeNames = SqsAttributes.All,
			}, token);

	public async Task<List<SqsResult<SendMessageBatchResultEntry>>> Send(
		List<SendMessageBatchRequestEntry> messages,
		CancellationToken token)
	{
		var response = await _client.SendMessageBatchAsync(
			new SendMessageBatchRequest {
				QueueUrl = _queueUrl,
				Entries = messages,
			}, token);
		return Combine(response.Successful, response.Failed);
	}

	public async Task<List<Message>> Receive(CancellationToken token)
	{
		var response = await _client.ReceiveMessageAsync(
			new ReceiveMessageRequest {
				QueueUrl = _queueUrl,
				MaxNumberOfMessages = SqsConstants.MaximumNumberOfMessages,
				AttributeNames = SqsAttributes.All, // ApproximateReceiveCount, SentTimestamp  
				MessageAttributeNames = SqsAttributes.All,
			}, token);
		return response.Messages;
	}

	public async Task<List<SqsResult<DeleteMessageBatchResultEntry>>> Delete(
		List<DeleteMessageBatchRequestEntry> messages,
		CancellationToken token)
	{
		var response = await _client.DeleteMessageBatchAsync(
			new DeleteMessageBatchRequest {
				QueueUrl = _queueUrl,
				Entries = messages,
			}, token);
		return Combine(response.Successful, response.Failed);
	}

	public async Task<List<SqsResult<ChangeMessageVisibilityBatchResultEntry>>> Touch(
		List<ChangeMessageVisibilityBatchRequestEntry> messages,
		CancellationToken token)
	{
		var response = await _client.ChangeMessageVisibilityBatchAsync(
			new ChangeMessageVisibilityBatchRequest {
				QueueUrl = _queueUrl,
				Entries = messages,
			}, token);
		return Combine(response.Successful, response.Failed);
	}

	private static List<SqsResult<T>> Combine<T>(
		IReadOnlyCollection<T> success,
		IReadOnlyCollection<BatchResultErrorEntry> failure)
	{
		var count = success.Count + failure.Count;
		var result = new List<SqsResult<T>>(count);
		result.AddRange(success.Select(r => new SqsResult<T>(r)));
		result.AddRange(failure.Select(r => new SqsResult<T>(r)));
		return result;
	}
}