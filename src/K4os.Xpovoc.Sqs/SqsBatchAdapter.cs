using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using K4os.Async.Toys;
using K4os.Xpovoc.Sqs.Internal;
using Microsoft.Extensions.Logging;

namespace K4os.Xpovoc.Sqs;

internal class SqsBatchAdapter: IBatchPoller<Message, string>
{
	protected readonly ILogger Log;

	private readonly ISqsQueue _queue;
	private readonly TimeSpan _visibility;

	public SqsBatchAdapter(ILogger log, ISqsQueue queue, TimeSpan visibility)
	{
		Log = log;
		_queue = queue;
		_visibility = visibility;
	}

	public string ReceiptFor(Message message) => message.ReceiptHandle;
	public string IdentityOf(string receipt) => receipt;

	public async Task<SqsResult<SendMessageBatchResultEntry>[]> Send(
		SendMessageBatchRequestEntry[] messages)
	{
		var requests = messages.ToList();
		Log.LogDebug("Sending batch of {Count} messages", requests.Count);
		var responses = await _queue.Send(requests, CancellationToken.None);
		return responses.ToArray();
	}

	public async Task<Message[]> Receive(CancellationToken token)
	{
		var result = await _queue.Receive(token);
		return result.ToArray();
	}

	public async Task<string[]> Delete(string[] receipts, CancellationToken token)
	{
		var (map, requests) = BuildRequestMap(receipts, ToDeleteRequest);
		Log.LogDebug("Deleting batch of {Count} messages", requests.Count);
		var response = await _queue.Delete(requests, token);
		return response
			.SelectNotNull(r => r.Result?.Id)
			.SelectNotNull(id => map.TryGetOrDefault(id))
			.ToArray();
	}

	public async Task<string[]> Touch(string[] receipts, CancellationToken token)
	{
		var timeout = (int)_visibility.TotalSeconds;
		var (map, requests) = BuildRequestMap(receipts, (id, m) => ToTouchRequest(id, m, timeout));
		Log.LogDebug("Touching batch of {Count} messages", requests.Count);
		var response = await _queue.Touch(requests, token);
		return response
			.SelectNotNull(r => r.Result?.Id)
			.SelectNotNull(id => map.TryGetOrDefault(id))
			.ToArray();
	}
	
	private static DeleteMessageBatchRequestEntry ToDeleteRequest(string id, string receipt) =>
		new() { Id = id, ReceiptHandle = receipt };

	private static ChangeMessageVisibilityBatchRequestEntry ToTouchRequest(
		string id, string receipt, int timeout) =>
		new() { Id = id, ReceiptHandle = receipt, VisibilityTimeout = timeout };

	private static (Dictionary<string, string> Receipts, List<T> Requests) BuildRequestMap<T>(
		ICollection<string> receipts, Func<string, string, T> toRequest)
	{
		var capacity = receipts.Count;
		var map = new Dictionary<string, string>(capacity);
		var list = new List<T>(capacity);
		var counter = 0;

		foreach (var receipt in receipts)
		{
			var itemId = (counter++).ToString();
			var request = toRequest(itemId, receipt);
			map.Add(itemId, receipt);
			list.Add(request);
		}

		return (map, list);
	}
}
