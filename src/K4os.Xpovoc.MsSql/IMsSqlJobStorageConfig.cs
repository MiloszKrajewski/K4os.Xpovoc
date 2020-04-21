namespace K4os.Xpovoc.MsSql
{
	public interface IMsSqlJobStorageConfig
	{
		string ConnectionString { get; }
		string Schema { get; }
	}
}
