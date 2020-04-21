using System;

namespace K4os.Xpovoc.SqLite
{
	public class SqLiteJobStorageConfig: ISqLiteJobStorageConfig
	{
		public string ConnectionString { get; set; }

		public string Prefix { get; set; }

		public int PoolSize { get; set; } = 1;
	}
}
