using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace K4os.Xpovoc.Sqs.Test;

public class TestLoggerFactory: ILoggerFactory
{
	private readonly ITestOutputHelper _output;

	public TestLoggerFactory(ITestOutputHelper output) { _output = output; }

	public ILogger CreateLogger(string categoryName) => new TestLogger(_output, categoryName);

	public void AddProvider(ILoggerProvider provider) { }

	public void Dispose() { }
}
