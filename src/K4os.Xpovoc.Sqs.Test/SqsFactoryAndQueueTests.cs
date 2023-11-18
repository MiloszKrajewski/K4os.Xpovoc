using Amazon.SQS;
using Amazon.SQS.Model;
using K4os.Xpovoc.Sqs.Internal;
using Xunit.Abstractions;

namespace K4os.Xpovoc.Sqs.Test;

public class SqsFactoryAndQueueTests
{
	private readonly ITestOutputHelper _testOutputHelper;

	public SqsFactoryAndQueueTests(ITestOutputHelper testOutputHelper)
	{
		_testOutputHelper = testOutputHelper;
	}

	[Fact]
	public async Task QueueCanBeCreatedOrFound()
	{
		var queueName = "mk-scheduler-test";
		var factory = new SqsQueueFactory(new AmazonSQSClient());
		var queue = await factory.Create(queueName, new SqsQueueSettings());
		Assert.NotNull(queue);
	}

	private static SendMessageBatchRequestEntry CreateMessage()
	{
		var id = Guid.NewGuid().ToString("N");
		var jitterFive = Random.Shared.NextDouble() * 5;
		return new SendMessageBatchRequestEntry {
			Id = id,
			MessageAttributes = new Dictionary<string, MessageAttributeValue> {
				{
					"Xpovoc-ScheduledFor",
					new MessageAttributeValue {
						DataType = "String",
						StringValue = DateTime.UtcNow.AddSeconds(jitterFive).ToString("O"),
					}
				},
			},
			MessageBody = $"message {id}",
		};
	}

	[Fact]
	public async Task MessagesCanBySent()
	{
		var factory = new SqsQueueFactory(new AmazonSQSClient());
		var queue = await factory.Create("mk-scheduler-test", new SqsQueueSettings());

		var response = await queue.Send(
			new List<SendMessageBatchRequestEntry> {
				CreateMessage(),
				CreateMessage(),
				CreateMessage(),
			}, CancellationToken.None);

		Assert.True(response.Count > 0);
		Assert.True(response.All(r => r.Error == null));
	}

	[Fact]
	public async Task MessagesCanBySentAndReceived()
	{
		var factory = new SqsQueueFactory(new AmazonSQSClient());
		var queue = await factory.Create("mk-scheduler-test", new SqsQueueSettings());

		await queue.Send(
			new List<SendMessageBatchRequestEntry> {
				CreateMessage(),
				CreateMessage(),
				CreateMessage(),
			}, CancellationToken.None);

		var messages = await queue.Receive(CancellationToken.None);
		var count = messages.Count;

		_testOutputHelper.WriteLine($"Received {count} messages");

		Assert.True(count > 0);
	}

	[Fact]
	public async Task MessagesCanBeDeleted()
	{
		var factory = new SqsQueueFactory(new AmazonSQSClient());
		var queue = await factory.Create("mk-scheduler-test", new SqsQueueSettings());

		await queue.Send(
			new List<SendMessageBatchRequestEntry> {
				CreateMessage(),
				CreateMessage(),
				CreateMessage(),
			}, CancellationToken.None);

		var messages = await queue.Receive(CancellationToken.None);

		var deletes = messages
			.Select(
				m => new DeleteMessageBatchRequestEntry {
					Id = Guid.NewGuid().ToString("N"), 
					ReceiptHandle = m.ReceiptHandle,
				})
			.ToList();

		var response = await queue.Delete(deletes, CancellationToken.None);
		var count = response.Count;
		
		_testOutputHelper.WriteLine($"Deleted {count} messages");
		
		Assert.True(response.Count == messages.Count);
		Assert.True(response.All(r => r.Error == null));
	}
	
	[Fact]
	public async Task MessagesCanBeTouched()
	{
		var factory = new SqsQueueFactory(new AmazonSQSClient());
		var queue = await factory.Create("mk-scheduler-test", new SqsQueueSettings());

		await queue.Send(
			new List<SendMessageBatchRequestEntry> {
				CreateMessage(),
				CreateMessage(),
				CreateMessage(),
			}, CancellationToken.None);

		var messages = await queue.Receive(CancellationToken.None);

		var touches = messages
			.Select(
				m => new ChangeMessageVisibilityBatchRequestEntry {
					Id = Guid.NewGuid().ToString("N"), 
					ReceiptHandle = m.ReceiptHandle,
					VisibilityTimeout = 1,
				})
			.ToList();

		var response = await queue.Touch(touches, CancellationToken.None);
		var count = response.Count;
		
		_testOutputHelper.WriteLine($"Touched {count} messages");
		
		Assert.True(response.Count == messages.Count);
		Assert.True(response.All(r => r.Error == null));
	}

}
