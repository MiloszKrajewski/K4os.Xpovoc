using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.Core.Memory;

namespace K4os.Xpovoc.Db.Test.Integrations
{
	public class MemoryStorageTest: StorageTestBase
	{
		private MemoryJobStorage _storage;

		protected override IDbJobStorage CreateStorage(string schema) =>
			_storage = new MemoryJobStorage();

		protected override void ClearStorage(string schema) => 
			_storage = null;

		protected override int CountJobs(string schema)
		{
			return _storage.Size;
		}
	}
}
