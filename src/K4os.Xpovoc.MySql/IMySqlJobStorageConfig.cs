namespace K4os.Xpovoc.MySql
{
	public interface IMySqlJobStorageConfig
	{
		string ConnectionString { get; set; }
		string TablePrefix { get; set; }
	}
}
