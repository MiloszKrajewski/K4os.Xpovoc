using System;

namespace K4os.Xpovoc.Toolbox.Sql
{
	public interface ILease<out T>: IDisposable
	{
		T Connection { get; }
	}
}
