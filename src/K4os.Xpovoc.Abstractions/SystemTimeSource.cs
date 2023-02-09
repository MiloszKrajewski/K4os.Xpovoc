using System;

namespace K4os.Xpovoc.Abstractions;

public class SystemDateTimeSource: IDateTimeSource
{
	public static readonly IDateTimeSource Default = new SystemDateTimeSource();
	public DateTimeOffset Now => DateTimeOffset.UtcNow;
	private SystemDateTimeSource() { }
}