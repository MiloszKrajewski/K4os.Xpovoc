namespace K4os.Xpovoc.Mongo;

public class MongoJobStorageConfig: IMongoJobStorageConfig
{
	public string? ConnectionString { get; set; }
	public string? DatabaseName { get; set; }
	public string? CollectionName { get; set; }
}