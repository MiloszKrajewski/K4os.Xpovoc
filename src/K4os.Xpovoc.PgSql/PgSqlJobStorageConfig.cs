using System;

namespace K4os.Xpovoc.PgSql;

public class PgSqlJobStorageConfig: IPgSqlJobStorageConfig
{
	public string ConnectionString { get; set; } = string.Empty;

	public string? Schema { get; set; }
}