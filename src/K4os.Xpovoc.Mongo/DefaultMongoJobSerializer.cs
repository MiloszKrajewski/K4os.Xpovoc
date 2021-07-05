using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using MongoDB.Bson;

namespace K4os.Xpovoc.Mongo
{
	public class DefaultMongoJobSerializer: IMongoJobSerializer
	{
		#pragma warning disable 618
		public static readonly DefaultMongoJobSerializer Instance = new();
		#pragma warning restore 618

		[Obsolete("Use .Instance instead")]
		public DefaultMongoJobSerializer() { }

		public BsonValue Serialize(object? payload)
		{
			if (payload is null) 
				return BsonNull.Value;

			var formatter = new BinaryFormatter();
			using var stream = new MemoryStream();
			formatter.Serialize(stream, payload);
			return new BsonBinaryData(stream.ToArray());
		}

		public object Deserialize(BsonValue payload) =>
			payload switch {
				null or BsonNull => null!, // should not, but can
				BsonBinaryData { Bytes: var bytes } => Deserialize(bytes),
				var x => throw new InvalidDataException(
					$"Serialized job cannot be decoded from {x.GetType().Name}")
			};

		private static object Deserialize(byte[] bytes)
		{
			var formatter = new BinaryFormatter();
			using var stream = new MemoryStream(bytes);
			return formatter.Deserialize(stream);
		}
	}
}
