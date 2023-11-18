using System.IO;
using System.Xml.Linq;

// ReSharper disable once CheckNamespace
namespace System;

internal class Secrets
{
	public static XDocument? Load(string filename) => Load(".", filename);

	public static XDocument? Load(string root, string filename)
	{
		var combined = Path.Combine(root, filename);
		if (File.Exists(combined)) return XDocument.Load(combined);

		var parent = Path.Combine(root, "..");
		return IsSamePath(root, parent) ? null : Load(parent, filename);
	}

	private static bool IsSamePath(string pathA, string pathB) => 
		Path.GetFullPath(pathA) == Path.GetFullPath(pathB);
}