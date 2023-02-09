using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace K4os.Xpovoc.Core;

public class ResourceLoader
{
	protected static XElement GetEmbeddedXml(Type type, string resourceName)
	{
		var assembly = type.Assembly;
		using var stream =
			assembly.GetManifestResourceStream(type, resourceName) ??
			throw new ArgumentException(
				$"Embedded stream {resourceName} for {type.Name} could not be found");

		using var reader = new StreamReader(stream);
		return XElement.Parse(reader.ReadToEnd());
	}

	protected static XElement GetEmbeddedXml<THook>(string resourceName) =>
		GetEmbeddedXml(typeof(THook), resourceName);

	protected XElement GetEmbeddedXml(string resourceName) =>
		GetEmbeddedXml(GetType(), resourceName);

	protected static string GetId(XElement element) =>
		element.Attribute("id").Required("id").Value;
}