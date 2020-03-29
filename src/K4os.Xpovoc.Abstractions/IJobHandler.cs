using System;
using System.Threading;
using System.Threading.Tasks;

namespace K4os.Xpovoc.Abstractions
{
	public interface IJobHandler
	{
		Task Handle(CancellationToken token, object payload);
	}
}
