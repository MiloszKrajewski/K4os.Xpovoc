using System;

namespace K4os.Xpovoc.MySql
{
	public class MySqlJobStorageConfig: IMySqlJobStorageConfig
	{
		public string ConnectionString { get; set; }

		public string Prefix { get; set; }
	}
}
