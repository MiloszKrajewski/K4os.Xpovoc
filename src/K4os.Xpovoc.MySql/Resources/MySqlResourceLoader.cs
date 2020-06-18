using System;
using System.Collections.Generic;
using System.Linq;
using K4os.Xpovoc.Core;
using K4os.Xpovoc.Core.Sql;

namespace K4os.Xpovoc.MySql.Resources
{
	public class MySqlResourceLoader: ResourceLoader
	{
		public static readonly MySqlResourceLoader Default = new MySqlResourceLoader();

		public IEnumerable<IMigration> LoadMigrations(string prefix) => 
			AnySqlResourceLoader.LoadMigrations(
				GetEmbeddedXml("Migrations.xml"), 
				s => FixPrefix(prefix, s));

		public IDictionary<string, string> LoadQueries(string prefix) =>
			AnySqlResourceLoader.LoadQueries(
				GetEmbeddedXml("Queries.xml"), 
				s => FixPrefix(prefix, s));
		
		private static string FixPrefix(string prefix, string text) => 
			text.Replace("{prefix}", prefix);
	}
}
