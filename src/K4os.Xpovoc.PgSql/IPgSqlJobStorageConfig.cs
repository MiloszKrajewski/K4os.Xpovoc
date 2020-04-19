namespace K4os.Xpovoc.PgSql
{
	public interface IPgSqlJobStorageConfig
	{
		string ConnectionString { get; }
		string Schema { get; }
	}
}
