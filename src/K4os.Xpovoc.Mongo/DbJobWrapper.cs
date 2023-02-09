using System;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.Mongo.Model;

namespace K4os.Xpovoc.Mongo;

public class DbJobWrapper: IDbJob
{
	private readonly JobDocument _job;
	private readonly object _payload;

	internal DbJobWrapper(JobDocument job, object payload)
	{
		_job = job;
		_payload = payload;
	}

	public Guid JobId => _job.JobId;
	public DateTime UtcTime => _job.ScheduledFor;
	public object Payload => _payload;
	public object Context => _job;
	public int Attempt => _job.Attempt;
}