using System;
using System.Threading;
using System.Threading.Tasks;

namespace K4os.Xpovoc.Abstractions
{
	public interface IJobHandler
	{
		Task Handle(CancellationToken token, object payload);
	}

	public interface IJobHandler<in TMessage>
	{
		Task Handle(CancellationToken token, TMessage payload);
	}
}
