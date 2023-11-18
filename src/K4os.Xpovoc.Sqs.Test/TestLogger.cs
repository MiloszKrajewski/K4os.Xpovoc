using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace K4os.Xpovoc.Sqs.Test;

public class TestLogger: ILogger
{
	private readonly ITestOutputHelper _output;
	private readonly string _categoryName;

	public TestLogger(ITestOutputHelper output, string categoryName)
	{
		_output = output;
		_categoryName = categoryName;
	}

	public void Log<TState>(
		LogLevel logLevel,
		EventId eventId,
		TState state,
		Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
		_output.WriteLine(
			"[{0}] ({1}) {2}", logLevel, _categoryName, formatter(state, exception));
	}

	public bool IsEnabled(LogLevel logLevel) => true;

	public IDisposable? BeginScope<TState>(TState state) where TState: notnull => null;
}
