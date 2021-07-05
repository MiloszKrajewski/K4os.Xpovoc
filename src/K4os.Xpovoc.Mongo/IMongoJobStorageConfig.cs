namespace K4os.Xpovoc.Mongo
{
	public interface IMongoJobStorageConfig
	{
		string? ConnectionString { get; }
		string? DatabaseName { get; }
		string? CollectionName { get; }
	}
}
