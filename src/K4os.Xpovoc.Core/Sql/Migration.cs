namespace K4os.Xpovoc.Core.Sql;

public class Migration: IMigration
{
	public string Id { get; } = null!;
	public string Script { get; } = string.Empty;

	public Migration() { }

	public Migration(string id, string script) => (Id, Script) = (id, script);

	public Migration(IMigration migration): this(migration.Id, migration.Script) { }
}