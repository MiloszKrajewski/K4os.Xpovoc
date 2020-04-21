using System.IO;
using System.Xml.Linq;

// ReSharper disable once CheckNamespace
namespace System
{
	internal class Secrets
	{
		public static XDocument Load(string filename) => Load(".", filename);

		public static XDocument Load(string root, string filename)
		{
			var combined = Path.Combine(root, filename);
			if (File.Exists(combined)) return XDocument.Load(combined);

			var parent = Path.Combine(root, "..");
			if (IsSamePath(root, parent))
				return null;

			return Load(parent, filename);
		}

		private static bool IsSamePath(string root, string parent)
		{
			return Path.GetFullPath(root) == Path.GetFullPath(parent);
		}
	}
}
