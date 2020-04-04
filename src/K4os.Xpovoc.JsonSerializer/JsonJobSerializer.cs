using System;
using K4os.Xpovoc.Abstractions;
using Newtonsoft.Json;

namespace K4os.Xpovoc.JsonSerializer
{
	public class JsonJobSerializer: IJobSerializer
	{
		private readonly JsonSerializerSettings _settings;

		public JsonJobSerializer(JsonSerializerSettings settings)
		{
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));
		}

		public string Serialize(object job) =>
			JsonConvert.SerializeObject(job, typeof(object), _settings);

		public object Deserialize(string payload) =>
			JsonConvert.DeserializeObject(payload, _settings);
	}
}
