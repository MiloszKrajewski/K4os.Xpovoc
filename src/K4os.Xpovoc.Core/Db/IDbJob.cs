using K4os.Xpovoc.Abstractions;

namespace K4os.Xpovoc.Core.Db;

public interface IDbJob: IJob
{
	int Attempt { get; }
}