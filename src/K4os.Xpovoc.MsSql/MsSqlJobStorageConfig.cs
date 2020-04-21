using System;

namespace K4os.Xpovoc.MsSql
{
	public class MsSqlJobStorageConfig: IMsSqlJobStorageConfig
	{
		public string ConnectionString { get; set; }

		public string Schema { get; set; }
	}
}
