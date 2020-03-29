namespace K4os.Xpovoc.AnySql
{
	public class Migration: IMigration
	{
		public string Id { get; }
		public string Script { get; }

		public Migration() { }

		public Migration(string id, string script) => (Id, Script) = (id, script);

		public Migration(IMigration migration): this(migration.Id, migration.Script) { }
	}
}
