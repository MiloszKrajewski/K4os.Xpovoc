using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace K4os.Xpovoc.Core.Sql;

public abstract class AnySqlMigrator
{
	private readonly string _productId;
	private readonly IDbConnection _connection;
	private readonly Migration[] _migrations;

	protected AnySqlMigrator(
		string productId,
		IDbConnection connection,
		IEnumerable<IMigration> migrations)
	{
		_productId = productId.Required(nameof(productId));
		_connection = connection.Required(nameof(connection));
		_migrations = migrations
			.Required(nameof(migrations))
			.Select(m => new Migration(m))
			.ToArray();
	}

	public void Install()
	{
		EnsureMigrationsTable(_connection);

		foreach (var migration in _migrations)
		{
			var migrationId = $"{_productId}/{migration.Id}";
			var alreadyApplied = IsMigrationApplied(_connection, migrationId);
			if (alreadyApplied) continue;

			ApplyMigration(_connection, migrationId, migration.Script);
		}
	}

	private void EnsureMigrationsTable(IDbConnection connection)
	{
		var tableExists = MigrationTableExists(connection);
		if (tableExists) return;

		CreateMigrationTable(connection);
	}

	private void ApplyMigration(IDbConnection connection, string id, string script)
	{
		// NOTE: Some operations cannot be executed in transactions (create table?)
		// we will need some mechanism to handle that if we need it
		using var transaction = connection.BeginTransaction();
		ExecuteScript(connection, transaction, script);
		MarkMigrationDone(connection, transaction, id);
		transaction.Commit();
	}

	protected abstract void ExecuteScript(
		IDbConnection connection, IDbTransaction transaction, string script);

	protected abstract bool MigrationTableExists(IDbConnection connection);

	protected abstract void CreateMigrationTable(IDbConnection connection);

	protected abstract bool IsMigrationApplied(IDbConnection connection, string id);

	protected abstract void MarkMigrationDone(
		IDbConnection connection, IDbTransaction transaction, string id);
}