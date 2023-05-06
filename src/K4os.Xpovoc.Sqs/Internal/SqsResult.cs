using Amazon.SQS.Model;

namespace K4os.Xpovoc.Sqs.Internal;

public readonly record struct SqsResult<T>(
	T? Result, BatchResultErrorEntry? Error)
{
	public SqsResult(T result): this(result, null) { }
	public SqsResult(BatchResultErrorEntry error): this(default, error) { }

	public bool IsSuccess => !IsError;
	public bool IsError => Error != null;
}
