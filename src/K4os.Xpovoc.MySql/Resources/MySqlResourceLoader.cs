using System;
using System.Collections.Generic;
using System.Linq;
using K4os.Xpovoc.AnySql;

namespace K4os.Xpovoc.MySql.Resources
{
	public class MySqlResourceLoader: ResourceLoader
	{
		public static readonly MySqlResourceLoader Default = new MySqlResourceLoader();

		public IEnumerable<IMigration> LoadMigrations(string tablePrefix) => 
			LoadMigrations(GetEmbeddedXml("Migrations.xml"), tablePrefix);

		public Dictionary<string, string> LoadQueries(string tablePrefix) =>
			LoadQueries(GetEmbeddedXml("Queries.xml"), tablePrefix);
	}
}
