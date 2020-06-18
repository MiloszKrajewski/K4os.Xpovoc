using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.Core.Memory;

namespace K4os.Xpovoc.Db.Test.Integrations
{
	public class MemoryStorageTest: StorageTestBase
	{
		protected override IDbJobStorage CreateStorage(string schema) =>
			new MemoryJobStorage();

		protected override void ClearStorage(string schema) { }
	}
}
