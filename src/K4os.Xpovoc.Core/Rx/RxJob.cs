using System;

namespace K4os.Xpovoc.Core.Rx;

internal class RxJob
{
	public IDisposable Schedule { get; set; } = null!;
	public Guid Id { get; set; }
	public object? Payload { get; set; }
	public int Attempt { get; set; }
}