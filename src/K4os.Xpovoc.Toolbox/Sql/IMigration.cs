namespace K4os.Xpovoc.Toolbox.Sql
{
	public interface IMigration
	{
		string Id { get; }
		string Script { get; }
	}
}
