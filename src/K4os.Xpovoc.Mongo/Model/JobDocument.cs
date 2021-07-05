using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace K4os.Xpovoc.Mongo.Model
{
	internal class JobDocument
	{
		[BsonIgnore]
		internal const int CurrentProtocol = 1;
		
		[BsonId, BsonRepresentation(BsonType.ObjectId)]
		public ObjectId RowId { get; set; }

		[BsonElement("protocol"), BsonRepresentation(BsonType.Int32)]
		public int Protocol { get; set; } = CurrentProtocol;

		[BsonElement("job_id"), BsonRepresentation(BsonType.String)]
		public Guid JobId { get; set; }

		[BsonElement("claimed_by"), BsonRepresentation(BsonType.String)]
		public Guid? ClaimedBy { get; set; }

		[BsonElement("scheduled_for"), BsonRepresentation(BsonType.DateTime)]
		public DateTime ScheduledFor { get; set; }

		[BsonElement("invisible_until"), BsonRepresentation(BsonType.DateTime)]
		public DateTime InvisibleUntil { get; set; }

		[BsonElement("attempt"), BsonRepresentation(BsonType.Int32)]
		public int Attempt { get; set; }

		[BsonElement("status"), BsonRepresentation(BsonType.Int32)]
		public JobStatus Status { get; set; }

		[BsonElement("payload")]
		public BsonValue Payload { get; set; } = BsonNull.Value;
	}
}
