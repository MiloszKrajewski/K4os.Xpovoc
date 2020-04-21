using System;
using System.Collections.Generic;
using System.Linq;
using K4os.Xpovoc.Core;
using K4os.Xpovoc.Core.Sql;

namespace K4os.Xpovoc.SqLite.Resources
{
	public class SqLiteResourceLoader: ResourceLoader
	{
		public static readonly SqLiteResourceLoader Default = new SqLiteResourceLoader();

		public IEnumerable<IMigration> LoadMigrations(string prefix) =>
			LoadMigrations(
				GetEmbeddedXml("Migrations.xml"),
				s => Update(prefix, s));

		public Dictionary<string, string> LoadQueries(string prefix) =>
			LoadQueries(
				GetEmbeddedXml("Queries.xml"),
				s => Update(prefix, s));

		private static string Update(string prefix, string text) =>
			text.Replace("{prefix}", prefix);
		
	}
}
