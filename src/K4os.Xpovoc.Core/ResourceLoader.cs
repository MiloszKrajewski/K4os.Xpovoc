using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using K4os.Xpovoc.Core.Sql;

namespace K4os.Xpovoc.Core
{
	public class ResourceLoader
	{
		protected static XElement GetEmbeddedXml(Type type, string resourceName)
		{
			var assembly = type.Assembly;
			using (var stream = assembly.GetManifestResourceStream(type, resourceName))
			{
				if (stream is null)
					throw new ArgumentException(
						$"Embedded stream {resourceName} for {type.Name} could not be found");

				using (var reader = new StreamReader(stream))
				{
					return XElement.Parse(reader.ReadToEnd());
				}
			}
		}

		protected static XElement GetEmbeddedXml<THook>(string resourceName) =>
			GetEmbeddedXml(typeof(THook), resourceName);

		protected XElement GetEmbeddedXml(string resourceName) =>
			GetEmbeddedXml(GetType(), resourceName);

		public static IEnumerable<IMigration> LoadMigrations(
			XElement xml, Func<string, string> update)
		{
			Migration ParseElement(XElement node) =>
				new Migration(GetId(node), Update(update, node.Value));

			return xml.Elements("migration").Select(ParseElement).ToArray();
		}

		public static Dictionary<string, string> LoadQueries(
			XElement xml, Func<string, string> update) =>
			xml.Elements("query").ToDictionary(GetId, e => Update(update, e.Value));

		protected static string GetId(XElement element) =>
			element.Attribute("id").Required("id").Value;

		private static string Update(Func<string, string> update, string text) =>
			update is null ? text : update(text);
	}
}
