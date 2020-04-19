using System;
using System.Collections.Generic;
using System.Linq;
using K4os.Xpovoc.Toolbox;
using K4os.Xpovoc.Toolbox.Sql;

namespace K4os.Xpovoc.PgSql.Resources
{
	public class PgSqlResourceLoader: ResourceLoader
	{
		public static readonly PgSqlResourceLoader Default = new PgSqlResourceLoader();

		public IEnumerable<IMigration> LoadMigrations(string schema) =>
			LoadMigrations(
				GetEmbeddedXml("Migrations.xml"),
				s => Update(schema, s));

		public Dictionary<string, string> LoadQueries(string schema) =>
			LoadQueries(
				GetEmbeddedXml("Queries.xml"),
				s => Update(schema, s));

		private static string Update(string schema, string text) =>
			text.Replace(
				"{schema}", 
				string.IsNullOrWhiteSpace(schema) ? string.Empty : $"\"{schema}\".");
		
	}
}
