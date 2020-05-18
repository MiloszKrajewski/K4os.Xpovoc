using System;
using K4os.Xpovoc.Core.Db;

namespace K4os.Xpovoc.Core.Sql
{
	public class SqlJob: IDbJob
	{
		public long RowId { get; }
		public Guid JobId { get; }
		public DateTime UtcTime { get; set; }
		public object Payload { get; }
		public object Context => this;
		public int Attempt { get; }

		public SqlJob(long rowId, Guid jobId, DateTime time, object payload, int attempt) =>
			(RowId, JobId, UtcTime, Payload, Attempt) = (rowId, jobId, time, payload, attempt);
	}
}
