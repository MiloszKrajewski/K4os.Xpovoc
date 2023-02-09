using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace K4os.Xpovoc.Core.Sql;

public abstract class AnySqlResourceLoader: ResourceLoader
{
	public static IEnumerable<IMigration> LoadMigrations(
		XElement xml, Func<string, string> update)
	{
		Migration ParseElement(XElement node) =>
			new Migration(GetId(node), Update(update, node.Value));

		return xml.Elements("migration").Select(ParseElement).ToArray();
	}

	public static IDictionary<string, string> LoadQueries(
		XElement xml, Func<string, string> update) =>
		xml.Elements("query").ToDictionary(GetId, e => Update(update, e.Value));
		
	private static string Update(Func<string, string> update, string text) =>
		update is null ? text : update(text);
}