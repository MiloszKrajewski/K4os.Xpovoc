namespace K4os.Xpovoc.MySql
{
	public interface IMySqlJobStorageConfig
	{
		string ConnectionString { get; }
		string? Prefix { get; }
	}
}
