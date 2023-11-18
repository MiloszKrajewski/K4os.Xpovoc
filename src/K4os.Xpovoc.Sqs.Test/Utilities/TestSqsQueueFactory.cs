using System.Reactive.Concurrency;
using K4os.Xpovoc.Sqs.Internal;

namespace K4os.Xpovoc.Sqs.Test.Utilities;

internal class TestSqsQueueFactory: ISqsQueueFactory
{
	private readonly Dictionary<string, TestSqsQueue> _queues = new();
	private readonly IScheduler _scheduler;

	public TestSqsQueueFactory(IScheduler scheduler)
	{
		_scheduler = scheduler;
	}
	
	public TestSqsQueue Create(string queueName, ISqsQueueSettings settings)
	{
		lock (_queues)
			return TryGetQueue(queueName) ?? (_queues[queueName] = CreateNewQueue(settings));
	}
	
	public TestSqsQueue? Find(string queueName)
	{
		lock (_queues) 
			return TryGetQueue(queueName);
	}

	Task<ISqsQueue> ISqsQueueFactory.Create(string queueName, ISqsQueueSettings settings) =>
		Task.FromResult<ISqsQueue>(Create(queueName, settings));
	
	private TestSqsQueue CreateNewQueue(ISqsQueueSettings settings) => 
		new(settings, _scheduler);
	
	private TestSqsQueue? TryGetQueue(string queueName) => 
		_queues.TryGetValue(queueName, out var queue) ? queue : null;
}