using System;
using System.Collections.Generic;
using System.Linq;
using K4os.Xpovoc.Toolbox;
using K4os.Xpovoc.Toolbox.Sql;

namespace K4os.Xpovoc.MySql.Resources
{
	public class MySqlResourceLoader: ResourceLoader
	{
		public static readonly MySqlResourceLoader Default = new MySqlResourceLoader();

		public IEnumerable<IMigration> LoadMigrations(string tablePrefix) => 
			LoadMigrations(
				GetEmbeddedXml("Migrations.xml"), 
				s => FixPrefix(tablePrefix, s));

		public Dictionary<string, string> LoadQueries(string tablePrefix) =>
			LoadQueries(
				GetEmbeddedXml("Queries.xml"), 
				s => FixPrefix(tablePrefix, s));
		
		private static string FixPrefix(string prefix, string text) => 
			text.Replace("{prefix}", prefix);
	}
}
