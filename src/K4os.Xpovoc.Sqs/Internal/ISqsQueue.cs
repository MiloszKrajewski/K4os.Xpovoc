using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;

namespace K4os.Xpovoc.Sqs.Internal;

public interface ISqsQueue
{
	Task<GetQueueAttributesResponse> GetAttributes(
		CancellationToken token = default);

	Task<List<SqsResult<SendMessageBatchResultEntry>>> Send(
		List<SendMessageBatchRequestEntry> messages,
		CancellationToken token);

	Task<List<Message>> Receive(CancellationToken token);

	Task<List<SqsResult<DeleteMessageBatchResultEntry>>> Delete(
		List<DeleteMessageBatchRequestEntry> messages,
		CancellationToken token);

	Task<List<SqsResult<ChangeMessageVisibilityBatchResultEntry>>> Touch(
		List<ChangeMessageVisibilityBatchRequestEntry> messages,
		CancellationToken token);
}
