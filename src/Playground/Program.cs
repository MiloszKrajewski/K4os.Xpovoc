using System;
using Amazon.SQS;
using Playground;
using Serilog;
using Serilog.Extensions.Logging;

Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Debug()
	.WriteTo.Console()
	.CreateLogger();
var loggerFactory = new SerilogLoggerFactory();
var sqsClient = new AmazonSQSClient();

// await InfiniteScheduling.Run(loggerFactory);

var test = new TrueDeferredJobs(sqsClient, loggerFactory, TimeSpan.FromHours(1));
await test.RunAsync();
