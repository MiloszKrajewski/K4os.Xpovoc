using System;
using System.Threading;
using System.Threading.Tasks;

namespace K4os.Xpovoc.Abstractions;

public interface ITimeSource
{
	DateTimeOffset Now { get; }
	Task Delay(TimeSpan delay, CancellationToken token);
}