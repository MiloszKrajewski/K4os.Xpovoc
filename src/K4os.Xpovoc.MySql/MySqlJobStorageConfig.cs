using System;

namespace K4os.Xpovoc.MySql
{
	public class MySqlJobStorageConfig: IMySqlJobStorageConfig
	{
		public string ConnectionString { get; set; } = 
			"server=localhost;database=test;uid=test;pwd=test";

		public string? Prefix { get; set; }
	}
}
