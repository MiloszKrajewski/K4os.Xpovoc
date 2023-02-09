using System;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using Polly;

namespace K4os.Xpovoc.MySql;

internal class MySqlExecutionPolicy
{
	private readonly Random _rng = new Random();

	private double ExpRng(double limit)
	{
		const double em1 = Math.E - 1;
		double random;
		lock (_rng) random = _rng.NextDouble();
		return (Math.Exp(random) - 1) * limit / em1;
	}

	private TimeSpan RetryInterval(int attempt) =>
		attempt <= 4
			? TimeSpan.Zero
			: ExpRng((attempt - 4) * 15)
				.NotMoreThan(1000)
				.PipeTo(TimeSpan.FromMilliseconds);

	private AsyncPolicy DeadlockPolicy { get; }

	public MySqlExecutionPolicy()
	{
		DeadlockPolicy = Policy
			.Handle<MySqlException>(e => e.Number == 1213)
			.WaitAndRetryForeverAsync(RetryInterval);
	}

	public Task Undeadlock(
		MySqlConnection connection, Func<MySqlConnection, Task> action) =>
		Undeadlock(CancellationToken.None, connection, action);

	public Task<T> Undeadlock<T>(
		MySqlConnection connection, Func<MySqlConnection, Task<T>> action) =>
		Undeadlock(CancellationToken.None, connection, action);

	public Task Undeadlock(
		CancellationToken token, MySqlConnection connection,
		Func<MySqlConnection, Task> action) =>
		DeadlockPolicy.ExecuteAsync(
			() => {
				token.ThrowIfCancellationRequested();
				return action(connection);
			});

	public Task<T> Undeadlock<T>(
		CancellationToken token, MySqlConnection connection,
		Func<MySqlConnection, Task<T>> action) =>
		DeadlockPolicy.ExecuteAsync(
			() => {
				token.ThrowIfCancellationRequested();
				return action(connection);
			});
}