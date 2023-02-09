using System;

namespace K4os.Xpovoc.MsSql;

public class MsSqlJobStorageConfig: IMsSqlJobStorageConfig
{
	public string ConnectionString { get; set; } = string.Empty;

	public string? Schema { get; set; }
}