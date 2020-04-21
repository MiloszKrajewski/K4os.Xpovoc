using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using K4os.Xpovoc.Abstractions;

namespace K4os.Xpovoc.Core.Sql
{
	public class DefaultJobSerializer: IJobSerializer
	{
		protected virtual BinaryFormatter CreateFormatter() => 
			new BinaryFormatter();

		public string Serialize(object job)
		{
			var formatter = CreateFormatter();
			using (var stream = new MemoryStream())
			{
				formatter.Serialize(stream, job);
				stream.Flush();
				return Convert.ToBase64String(stream.ToArray());
			}
		}

		public object Deserialize(string payload)
		{
			var formatter = CreateFormatter();
			using (var stream = new MemoryStream(Convert.FromBase64String(payload)))
			{
				return formatter.Deserialize(stream);
			}
		}
	}
}
