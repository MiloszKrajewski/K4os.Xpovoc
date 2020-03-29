using System;

namespace K4os.Xpovoc.Abstractions
{
	public interface IDateTimeSource
	{
		DateTimeOffset Now { get; }
	}
}
