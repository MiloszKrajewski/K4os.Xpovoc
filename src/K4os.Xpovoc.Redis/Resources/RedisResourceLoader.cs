using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using K4os.Xpovoc.Core;

namespace K4os.Xpovoc.Redis.Resources;

public class RedisResourceLoader: ResourceLoader
{
	public static readonly RedisResourceLoader Default = new();

	public IDictionary<string, string> LoadFunctions() =>
		GetEmbeddedXml("Functions.xml")
			.Elements("function")
			.ToDictionary(GetId, e => Sanitize(e.Value));

	private static readonly Regex SanitizerPattern = 
		new(@"\s*[\r\n]+\s*", RegexOptions.Singleline); 

	private static string Sanitize(string body) => 
		SanitizerPattern.Replace(body, " ").Trim();
}