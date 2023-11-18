using System;
using System.Threading;
using System.Threading.Tasks;

namespace K4os.Xpovoc.Abstractions;

public class SystemTimeSource: ITimeSource
{
	public static readonly ITimeSource Default = new SystemTimeSource();
	public DateTimeOffset Now => DateTimeOffset.UtcNow;
	public Task Delay(TimeSpan delay, CancellationToken token) => Task.Delay(delay, token);
	private SystemTimeSource() { }
}