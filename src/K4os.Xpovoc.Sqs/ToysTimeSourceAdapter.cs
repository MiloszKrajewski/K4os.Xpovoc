using System;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;

namespace K4os.Xpovoc.Sqs;

internal class ToysTimeSourceAdapter: K4os.Async.Toys.ITimeSource
{
	private readonly ITimeSource _timeSource;
	public ToysTimeSourceAdapter(ITimeSource timeSource) => _timeSource = timeSource;
	public Task Delay(TimeSpan delay, CancellationToken token) => _timeSource.Delay(delay, token);
	public DateTimeOffset Now => _timeSource.Now;
}
