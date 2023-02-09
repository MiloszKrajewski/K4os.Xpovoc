using System;

namespace K4os.Xpovoc.Core.Sql;

public interface ILease<out T>: IDisposable
{
	T Connection { get; }
}