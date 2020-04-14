using System;
using System.Collections.Generic;
using System.Linq;
using K4os.Xpovoc.AnySql;

namespace K4os.Xpovoc.PgSql.Resources
{
	public class PgSqlResourceLoader: ResourceLoader
	{
		public static readonly PgSqlResourceLoader Default = new PgSqlResourceLoader();

		public IEnumerable<IMigration> LoadMigrations(string tablePrefix) => 
			LoadMigrations(GetEmbeddedXml("Migrations.xml"), tablePrefix);

		public Dictionary<string, string> LoadQueries(string tablePrefix) =>
			LoadQueries(GetEmbeddedXml("Queries.xml"), tablePrefix);
	}
}
