namespace K4os.Xpovoc.AnySql
{
	public interface IMigration
	{
		string Id { get; }
		string Script { get; }
	}
}
