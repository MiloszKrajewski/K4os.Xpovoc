namespace K4os.Xpovoc.SqLite
{
	public interface ISqLiteJobStorageConfig
	{
		string ConnectionString { get; }
		string Prefix { get; }
		int PoolSize { get; }
	}
}
