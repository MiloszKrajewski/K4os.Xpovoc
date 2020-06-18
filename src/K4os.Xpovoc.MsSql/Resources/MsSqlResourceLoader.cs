using System;
using System.Collections.Generic;
using System.Linq;
using K4os.Xpovoc.Core;
using K4os.Xpovoc.Core.Sql;

namespace K4os.Xpovoc.MsSql.Resources
{
	public class MsSqlResourceLoader: ResourceLoader
	{
		public static readonly MsSqlResourceLoader Default = new MsSqlResourceLoader();

		public IEnumerable<IMigration> LoadMigrations(string schema) =>
			AnySqlResourceLoader.LoadMigrations(
				GetEmbeddedXml("Migrations.xml"),
				s => Update(schema, s));

		public IDictionary<string, string> LoadQueries(string schema) =>
			AnySqlResourceLoader.LoadQueries(
				GetEmbeddedXml("Queries.xml"),
				s => Update(schema, s));

		private static string Update(string schema, string text) =>
			text.Replace(
				"{schema}", 
				string.IsNullOrWhiteSpace(schema) ? string.Empty : $"[{schema}].");
		
	}
}
