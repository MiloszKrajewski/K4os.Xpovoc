using System.Threading.Tasks;

namespace K4os.Xpovoc.Sqs.Internal;

public interface ISqsQueueFactory
{
	Task<ISqsQueue> Create(string queueName, SqsQueueSettings settings);
}