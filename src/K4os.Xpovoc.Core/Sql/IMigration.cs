namespace K4os.Xpovoc.Core.Sql
{
	public interface IMigration
	{
		string Id { get; }
		string Script { get; }
	}
}
