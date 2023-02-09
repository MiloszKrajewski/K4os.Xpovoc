using MongoDB.Bson;

namespace K4os.Xpovoc.Mongo;

public interface IMongoJobSerializer
{
	BsonValue Serialize(object payload);
	object Deserialize(BsonValue payload);
}